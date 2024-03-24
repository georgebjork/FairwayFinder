using FairwayFinder.Core.Features.Admin.UserManagement.Models.FormModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Admin.UserManagement.Models.ViewModels;

public class ManageUserViewModel {
    
    public List<ApplicationUser> Users { get; set; } = new();
    public List<UserInvitation> Invites { get; set; } = new();

    public UserInviteFormModel InviteFormModel { get; set; } = new();
}