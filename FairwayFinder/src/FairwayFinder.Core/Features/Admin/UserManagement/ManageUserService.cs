using FairwayFinder.Core.Features.Admin.UserManagement.Models;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Admin.UserManagement;

public interface IManageUsersService {
    Task<List<ApplicationUser>> GetUsers();
    
    // User Invites
    Task<List<UserInvitation>> GetInvites();
    Task<UserInvitation?> GetValidInvite(string inviteId);
    Task<UserInvitation> CreateAndSendInvite(string email, string registerBaseUrl);
    Task<ApplicationUser> PromoteAdmin(string userId);
    Task<ApplicationUser> RevokeAdmin(string userId);
    Task<bool> RevokeInvite(string inviteId);
    Task<IdentityResult> RemoveUser(string userId);
}

public class ManageUsersService : IManageUsersService {
    
    private readonly IUserRepository _userRepository;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSenderService _emailSenderService;
    private readonly ILogger<ManageUsersService> _logger;
    
    public ManageUsersService(IUserRepository userRepository, UserManager<ApplicationUser> userManager, IUsernameRetriever usernameRetriever, IEmailSenderService emailSenderService, ILogger<ManageUsersService> logger) {
        _userRepository = userRepository;
        _userManager = userManager;
        _usernameRetriever = usernameRetriever;
        _emailSenderService = emailSenderService;
        _logger = logger;
    }
    
    public async Task<List<ApplicationUser>> GetUsers() {
        return await _userRepository.GetUsers();
    }
    
    public async Task<List<UserInvitation>> GetInvites() {
        return await _userRepository.GetInvites();
    }
    
    public async Task<UserInvitation?> GetValidInvite(string inviteId) {
        return await _userRepository.GetInvite(inviteId);
    }

    public async Task<UserInvitation> CreateAndSendInvite(string email, string registerBaseUrl)
    {
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
        
        await _userRepository.Insert(invite);
        
        // Send email with invite link
        await _emailSenderService.SendRegisterEmailAsync(email, $"{registerBaseUrl}/register/{invite.invitation_identifier}");
        
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
        
        _logger.LogInformation($"{_usernameRetriever.Username} promoted {user.Email} to admin.");
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
        
        _logger.LogInformation($"{_usernameRetriever.Username} revoked admin for {user.Email}");
        return user;
    }

    public async Task<bool> RevokeInvite(string inviteId)
    {
        var invite = await GetValidInvite(inviteId);

        if (invite is null) return true;
        
        invite.is_deleted = true;
        invite.updated_by = _usernameRetriever.Email;
        invite.updated_on = DateTime.UtcNow;
        invite.claimed_on = DateTime.UtcNow;

        return await _userRepository.Update(invite);
    }

    public async Task<IdentityResult> RemoveUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user is null) throw new NullReferenceException("User not found"); 
        
        return await _userManager.DeleteAsync(user);
    }
}