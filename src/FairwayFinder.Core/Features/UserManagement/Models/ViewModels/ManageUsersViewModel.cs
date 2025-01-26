using FairwayFinder.Core.Features.UserManagement.Models.FormModels;
using FairwayFinder.Core.Identity;

namespace FairwayFinder.Core.UserManagement.Models.ViewModels;

public class ManageUsersViewModel
{
    public List<ApplicationUser> Users { get; set; } = [];
    public List<UserInvitation> Invites { get; set; } = [];
    public InviteUserFormModel InviteFormModel { get; set; } = new();
}