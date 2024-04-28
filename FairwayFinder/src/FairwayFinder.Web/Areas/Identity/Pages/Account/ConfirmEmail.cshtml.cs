#nullable disable

using System.Text;
using FairwayFinder.Core.Features.Admin.UserManagement;
using FairwayFinder.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace FairwayFinder.Web.Areas.Identity.Pages.Account
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IManageUsersService _manageUsersService;
        
        public bool IsConfirmed { get; set; }
        
        public ConfirmEmailModel(UserManager<ApplicationUser> userManager, IManageUsersService manageUsersService)
        {
            _userManager = userManager;
            _manageUsersService = manageUsersService;
        }
        
        
        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{userId}'.");
            }
            
            // Decode
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            
            IsConfirmed = await _manageUsersService.ConfirmEmail(user, code);
            
            return Page();
        }
    }
}
