using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.UserManagement.Models;
using FairwayFinder.Core.UserManagement.Respositories;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Core.UserManagement.Services;

public interface IUserManagementService
{
    public Task<string> GenerateUserName(string firstName, string lastName);
    public Task<List<ApplicationUser>> GetAllUsers();
    public Task<List<ApplicationUser>> GetAllConfirmedUsers();
    public Task<List<ApplicationUser>> GetAllPendingUsers();
    public Task<List<UserInvitation>> GetAllInvitedUsers();
    public Task<ApplicationUser?> ApproveUserEmail(string userId);
    public Task<ApplicationUser?> DisableUser(string userId);
    Task<ApplicationUser?> EnableUser(string userId);
    public Task<bool> InviteUser(string email);
    public Task<UserInvitation?> GetValidInvite(string inviteId);
    public Task<bool> RevokeInvite(string invitationCode);
}

public class UserManagementService : IUserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserManagementRepository _userManagementRepository;
    private readonly IUsernameRetriever _usernameRetriever;
    
    public UserManagementService(UserManager<ApplicationUser> user_manager, IUserManagementRepository user_management_repository, IUsernameRetriever usernameRetriever)
    {
        _userManager = user_manager;
        _userManagementRepository = user_management_repository;
        _usernameRetriever = usernameRetriever;
    }

    public async Task<string> GenerateUserName(string firstName, string lastName)
    {
        var default_username = $"{firstName.ToLower()}{lastName.ToLower()}";
        var existing_usernames = await _userManagementRepository.FindAllSimilarUsernames(default_username);
        
        // If it doesn't exist. Then we are good to use it.
        if (!existing_usernames.Contains(default_username)) return default_username;
        
        string generated_handle;
        var counter = 1;
        do // Keep checking with a number appended to see if it is good to use.
        {
            generated_handle = $"{default_username}{counter++}";
            
        } while (existing_usernames.Contains(generated_handle));
        
        // Return
        return generated_handle;
    }

    public async Task<List<ApplicationUser>> GetAllUsers()
    {
        return await _userManagementRepository.GetAllUsers();
    }

    public async Task<List<ApplicationUser>> GetAllConfirmedUsers()
    {
        return await _userManagementRepository.GetAllConfirmedUsers();
    }

    public async Task<List<ApplicationUser>> GetAllPendingUsers()
    {
        return await _userManagementRepository.GetAllPendingUsers();
    }

    public async Task<List<UserInvitation>> GetAllInvitedUsers()
    {
        return await _userManagementRepository.GetAllInvitedUsers();
    }

    public async Task<ApplicationUser?> ApproveUserEmail(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null) return null;
        
        user.EmailConfirmed = true;
        user.UpdatedOn = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return user;
    }

    public async Task<ApplicationUser?> DisableUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null) return null;
        
        user.UpdatedOn = DateTime.UtcNow;
        
        var utc_date = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _userManager.SetLockoutEndDateAsync(user!, new DateTimeOffset(utc_date));
        
        await _userManager.UpdateAsync(user);
        return user;
    }

    public async Task<ApplicationUser?> EnableUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null) return null;
        
        user.UpdatedOn = DateTime.UtcNow;
        
        await _userManager.SetLockoutEndDateAsync(user!, null);
        
        await _userManager.UpdateAsync(user);
        return user;
    }

    public async Task<bool> InviteUser(string email)
    {
        // Validate the email isn't being used.
        var user = await _userManager.FindByEmailAsync(email);

        // Email is in use
        if (user is not null) return false;
        
        var date = DateTime.UtcNow;
        var username = _usernameRetriever.Username;

        var invite = new UserInvitation
        {
            invitation_identifier = Guid.NewGuid().ToString(),
            sent_to_email = email,
            sent_by_user = username,
            expires_on = date.AddDays(30),
            created_on = date,
            created_by = username,
            updated_on = date,
            updated_by = username
        };
        
        var rv = await _userManagementRepository.Insert(invite);
        return rv >= 1;
    }

    public async Task<UserInvitation?> GetValidInvite(string inviteId)
    {
        return await _userManagementRepository.GetInvite(inviteId);    
    }

    public async Task<bool> RevokeInvite(string invitationCode)
    {
        var invite = await _userManagementRepository.GetInvite(invitationCode);

        var date = DateTime.UtcNow;
        invite!.is_deleted = true;
        invite.claimed_on = date;
        invite.updated_on = date;
        invite.updated_by = _usernameRetriever.Username;

        return await _userManagementRepository.Update(invite);
    }
}