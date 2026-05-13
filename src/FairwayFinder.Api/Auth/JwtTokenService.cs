using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FairwayFinder.Api.Auth;

public class JwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly SigningCredentials _signingCredentials;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(
        IOptions<JwtSettings> settings,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<JwtTokenService> logger)
    {
        _settings = settings.Value;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
            new(ClaimTypes.Surname, user.LastName ?? string.Empty),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: _signingCredentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public async Task<string> IssueRefreshTokenAsync(string userId, string? deviceName = null, CancellationToken cancellationToken = default)
    {
        var (token, hash) = GenerateRefreshTokenPair();

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
            DeviceName = deviceName
        });
        await db.SaveChangesAsync(cancellationToken);

        return token;
    }

    /// <summary>
    /// Rotates a refresh token: marks the presented token as replaced and issues a new one.
    /// Returns null if the token is invalid, expired, or revoked. If a previously-rotated
    /// token is presented (reuse), revokes the entire token family for that user.
    /// </summary>
    public async Task<RefreshResult?> RotateRefreshTokenAsync(string presentedToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
            return null;

        var presentedHash = HashToken(presentedToken);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == presentedHash, cancellationToken);

        if (existing is null)
            return null;

        // Reuse detection: the token was already rotated. Treat as compromise and
        // revoke every active token for this user.
        if (existing.RevokedAt is not null)
        {
            _logger.LogWarning(
                "Refresh token reuse detected for user {UserId}. Revoking all active tokens.",
                existing.UserId);

            await RevokeAllForUserInternalAsync(db, existing.UserId, cancellationToken);
            return null;
        }

        if (existing.ExpiresAt <= DateTime.UtcNow)
            return null;

        var (newToken, newHash) = GenerateRefreshTokenPair();

        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedByTokenHash = newHash;

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = newHash,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
            DeviceName = existing.DeviceName
        });

        await db.SaveChangesAsync(cancellationToken);

        return new RefreshResult(existing.UserId, newToken);
    }

    public async Task<bool> RevokeRefreshTokenAsync(string presentedToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
            return false;

        var presentedHash = HashToken(presentedToken);

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == presentedHash && t.RevokedAt == null, cancellationToken);

        if (existing is null)
            return false;

        existing.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RevokeAllForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await RevokeAllForUserInternalAsync(db, userId, cancellationToken);
    }

    private static async Task RevokeAllForUserInternalAsync(ApplicationDbContext db, string userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    private static (string Token, string Hash) GenerateRefreshTokenPair()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (token, HashToken(token));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

public record RefreshResult(string UserId, string RefreshToken);
