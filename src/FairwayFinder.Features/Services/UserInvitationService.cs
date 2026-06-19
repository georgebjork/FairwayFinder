using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Services;

public class UserInvitationService : IUserInvitationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<UserInvitationService> _logger;
    private readonly string _registrationUrlBase;
    private readonly string? _appInstallUrl;

    private const int InviteValidityDays = 14;

    public UserInvitationService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<UserInvitationService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
        _registrationUrlBase = configuration["Invites:RegistrationUrlBase"]
            ?? "https://fairwayfinder.pro/register";
        // Optional: a TestFlight/App Store link offered to recipients who don't have the app yet.
        // Override via appsettings or the Invites__AppInstallUrl environment variable.
        _appInstallUrl = configuration["Invites:AppInstallUrl"];
    }

    public async Task<CreateInviteResult> CreateAndSendInviteAsync(string email, string sentByUserId)
    {
        email = email.Trim();

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
            return new CreateInviteResult { Success = false, Error = "An account with that email already exists." };

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var hasActiveInvite = await dbContext.UserInvitations.AnyAsync(i =>
            i.SentToEmail == email
            && !i.IsDeleted
            && i.ClaimedOn == null
            && i.ExpiresOn >= today);

        if (hasActiveInvite)
            return new CreateInviteResult { Success = false, Error = "An active invitation already exists for that email." };

        var invitation = new UserInvitation
        {
            InvitationIdentifier = Guid.NewGuid().ToString("N"),
            SentToEmail = email,
            SentByUser = sentByUserId,
            IsDeleted = false,
            ClaimedOn = null,
            ExpiresOn = today.AddDays(InviteValidityDays),
            CreatedBy = sentByUserId,
            CreatedOn = today,
            UpdatedBy = sentByUserId,
            UpdatedOn = today
        };

        dbContext.UserInvitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        var link = $"{_registrationUrlBase}?code={invitation.InvitationIdentifier}";

        try
        {
            await _emailSender.SendInvitationEmailAsync(email, link, _appInstallUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invitation email to {Email}", email);
            return new CreateInviteResult { Success = false, Error = "The invitation was created but the email failed to send." };
        }

        return new CreateInviteResult { Success = true };
    }

    public async Task<InviteValidationResult> ValidateInviteAsync(string code)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var invite = await dbContext.UserInvitations
            .FirstOrDefaultAsync(i => i.InvitationIdentifier == code && !i.IsDeleted);

        if (invite is null)
            return new InviteValidationResult { Valid = false, Reason = "This invitation link is not valid." };

        if (invite.ClaimedOn is not null)
            return new InviteValidationResult { Valid = false, Reason = "This invitation has already been used." };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (invite.ExpiresOn < today)
            return new InviteValidationResult { Valid = false, Reason = "This invitation has expired." };

        return new InviteValidationResult { Valid = true, Email = invite.SentToEmail };
    }

    public async Task<bool> ClaimInviteAsync(string code)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var invite = await dbContext.UserInvitations
            .FirstOrDefaultAsync(i => i.InvitationIdentifier == code
                && !i.IsDeleted
                && i.ClaimedOn == null
                && i.ExpiresOn >= today);

        if (invite is null)
            return false;

        invite.ClaimedOn = today;
        invite.UpdatedOn = today;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<PendingInviteDto>> GetPendingInvitesAsync()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await dbContext.UserInvitations
            .Where(i => !i.IsDeleted)
            .OrderByDescending(i => i.CreatedOn)
            .ThenByDescending(i => i.Id)
            .Select(i => new PendingInviteDto
            {
                Id = i.Id,
                Email = i.SentToEmail,
                CreatedOn = i.CreatedOn,
                ExpiresOn = i.ExpiresOn,
                ClaimedOn = i.ClaimedOn,
                IsExpired = i.ClaimedOn == null && i.ExpiresOn < today
            })
            .ToListAsync();
    }

    public async Task<bool> RevokeInviteAsync(int id, string revokedByUserId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var invite = await dbContext.UserInvitations
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

        if (invite is null)
            return false;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        invite.IsDeleted = true;
        invite.UpdatedBy = revokedByUserId;
        invite.UpdatedOn = today;
        await dbContext.SaveChangesAsync();
        return true;
    }
}
