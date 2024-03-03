using FairwayFinder.Core.Features.UserMangement.Models;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Settings;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Core.Features.UserMangement;

public interface IManageUsersService {
    Task<List<ApplicationUser>> GetUsers();
    
    // User Invites
    Task<List<user_invitation>> GetInvites();
    Task<user_invitation?> GetValidInvite(string inviteId);

    Task<user_invitation> CreateInvite(string email);

    Task<ApplicationUser> PromoteAdmin(string userId);
    Task<ApplicationUser> RevokeAdmin(string userId);
    Task<bool> RevokeInvite(string inviteId);
    
    Task<IdentityResult> RemoveUser(string userId);
}

public class ManageUsersService : IManageUsersService {
    
    private readonly IUserRepository _userRepository;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly UserManager<ApplicationUser> _userManager;
    
    public ManageUsersService(IUserRepository userRepository, UserManager<ApplicationUser> userManager, IUsernameRetriever usernameRetriever) {
        _userRepository = userRepository;
        _userManager = userManager;
        _usernameRetriever = usernameRetriever;
    }
    
    public async Task<List<ApplicationUser>> GetUsers() {
        return await _userRepository.GetUsers();
    }
    
    public async Task<List<user_invitation>> GetInvites() {
        return await _userRepository.GetInvites();
    }
    
    public async Task<user_invitation?> GetValidInvite(string inviteId) {
        return await _userRepository.GetInvite(inviteId);
    }

    public async Task<user_invitation> CreateInvite(string email)
    {
        var date = DateTime.UtcNow;
        var username = _usernameRetriever.Username;
        
        var invite = new user_invitation
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
        
        await _userRepository.Insert(invite);
        return invite;
    }

    public async Task<ApplicationUser> PromoteAdmin(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null) throw new NullReferenceException("User not found");
        
        // Add to role
        if (await _userManager.IsInRoleAsync(user, Roles.Admin)) return user;
        
        await _userManager.AddToRoleAsync(user, Roles.Admin);
        user.IsAdmin = true;
        
        return user;
    }

    public async Task<ApplicationUser> RevokeAdmin(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null) throw new NullReferenceException("User not found");
        
        // Remove from role and make sure they're in the user role
        await _userManager.RemoveFromRoleAsync(user, Roles.Admin);
        
        if(!await _userManager.IsInRoleAsync(user, Roles.User))
        {
            await _userManager.AddToRoleAsync(user, Roles.User);
        }

        user.IsAdmin = false;
        return user;
    }

    public async Task<bool> RevokeInvite(string inviteId)
    {
        var invite = await GetValidInvite(inviteId);

        if (invite is null) return true;
        
        invite.is_deleted = true;
        invite.updated_by = _usernameRetriever.Username;
        invite.updated_on = DateTime.UtcNow;

        return await _userRepository.Update(invite);
    }

    public async Task<IdentityResult> RemoveUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user is null) throw new NullReferenceException("User not found"); 
        
        return await _userManager.DeleteAsync(user);
    }
}
