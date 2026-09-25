using System.Text;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Services;

public class PasswordResetService : IPasswordResetService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<PasswordResetService> _logger;
    private readonly string _resetUrlBase;

    /// <summary>
    /// Shown for every redemption failure. Deliberately identical whether the email is unknown,
    /// the token is malformed, or the token has expired.
    /// </summary>
    private const string InvalidLinkMessage = "This password reset link is invalid or has expired.";

    public PasswordResetService(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<PasswordResetService> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
        // Points at the API host, which serves both the Apple app-site-association file and the
        // /reset-password landing page, so the link opens the app when it's installed and explains
        // itself when it isn't. Override with Auth__PasswordResetUrlBase.
        _resetUrlBase = configuration["Auth:PasswordResetUrlBase"]
                        ?? "https://api.fairwayfinder.pro/reset-password";
    }

    public async Task SendResetLinkAsync(string email)
    {
        email = email.Trim();

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            _logger.LogInformation("Password reset requested for an address with no account.");
            return;
        }

        if (!await _userManager.IsEmailConfirmedAsync(user))
        {
            _logger.LogInformation(
                "Password reset requested for {UserId}, whose email is not confirmed. No link sent.", user.Id);
            return;
        }

        try
        {
            await SendLinkAsync(user);
        }
        catch (Exception ex)
        {
            // Swallowed on purpose. Surfacing a send failure here would make the endpoint answer
            // differently for a real address than an unknown one, which is the leak the blanket
            // success response exists to prevent.
            _logger.LogError(ex, "Failed to send password reset email to {UserId}.", user.Id);
        }
    }

    public async Task<SendPasswordResetResult> SendResetLinkAsAdminAsync(string userId, string requestedByAdminUserId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return SendPasswordResetResult.Fail("That user no longer exists.");

        if (string.IsNullOrWhiteSpace(user.Email))
            return SendPasswordResetResult.Fail("That user has no email address on file.");

        // The golfer-facing path stays silent about this; an admin gets told, because they are the
        // one who can fix it.
        if (!await _userManager.IsEmailConfirmedAsync(user))
            return SendPasswordResetResult.Fail(
                "That user's email address is not confirmed, so a reset link can't be sent to it.");

        try
        {
            await SendLinkAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin {AdminUserId} failed to send a password reset email to {UserId}.",
                requestedByAdminUserId, user.Id);
            return SendPasswordResetResult.Fail("The reset email failed to send. Please try again.");
        }

        _logger.LogInformation("Admin {AdminUserId} sent a password reset link to {UserId}.",
            requestedByAdminUserId, user.Id);

        return SendPasswordResetResult.Ok();
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null)
            return ResetPasswordResult.Fail(InvalidLinkMessage);

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return ResetPasswordResult.Fail(InvalidLinkMessage);
        }

        var result = await _userManager.ResetPasswordAsync(user, decodedToken, newPassword);
        if (result.Succeeded)
        {
            // ResetPasswordAsync rolls the security stamp, which is what makes the link single-use.
            _logger.LogInformation("Password reset completed for {UserId}.", user.Id);
            return ResetPasswordResult.Ok();
        }

        // A rejected token and a rejected password are different failures to the user: one means
        // "get a new link", the other "pick a better password". Identity reports the bad token as
        // InvalidToken, so anything else is worth showing verbatim.
        if (result.Errors.Any(e => e.Code == "InvalidToken"))
            return ResetPasswordResult.Fail(InvalidLinkMessage);

        return ResetPasswordResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    private async Task SendLinkAsync(ApplicationUser user)
    {
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // Identity's token is opaque text containing characters that do not survive a query string
        // intact, so it travels base64url-encoded and is decoded again on redemption.
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var link = $"{_resetUrlBase}?email={Uri.EscapeDataString(user.Email!)}&token={encodedToken}";

        await _emailSender.SendPasswordResetEmailAsync(user.Email!, link);
    }
}
