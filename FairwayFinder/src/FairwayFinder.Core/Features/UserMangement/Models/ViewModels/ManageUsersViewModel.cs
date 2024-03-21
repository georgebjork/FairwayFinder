using FairwayFinder.Core.Features.UserMangement.Models.FormModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.UserMangement.Models.ViewModels;

public class ManageUserViewModel {
    
    public List<ApplicationUser> Users { get; set; } = new();
    public List<user_invitation> Invites { get; set; } = new();

    public UserInviteFormModel InviteFormModel { get; set; } = new();
}