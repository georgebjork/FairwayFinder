using System.ComponentModel.DataAnnotations;
using FairwayFinder.Core.Features.UserMangement;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FairwayFinder.Web.Areas.Identity.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserStore<ApplicationUser> _userStore;
    private readonly IUserEmailStore<ApplicationUser> _emailStore;
    private readonly ILogger<RegisterModel> _logger;
    private readonly IManageUsersService _userManagementService;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        IUserStore<ApplicationUser> userStore,
        SignInManager<ApplicationUser> signInManager,
        ILogger<RegisterModel> logger,
        IManageUsersService userManagementService)
    {
        _userManager = userManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _signInManager = signInManager;
        _logger = logger;
        _userManagementService = userManagementService;
    }
    
    [BindProperty]
    public InputModel Input { get; set; }
    
    public string ReturnUrl { get; set; }
    
    
    public class InputModel
    {
        public string InvitationCode{ get; set; }
       
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

       
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }
        
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
        
    }


    public async Task<IActionResult> OnGetAsync(string invitation, string returnUrl = null)
    {
        var invite = await _userManagementService.GetValidInvite(invitation);
        if (string.IsNullOrWhiteSpace(invite?.invitation_identifier)) {
            return RedirectToPage("./Login");
        }
        Input = new InputModel { InvitationCode = invitation, Email = invite.sent_to_email };
        ReturnUrl = returnUrl;
        Response.Cookies.Append("AccountInvitationCode", invitation, new CookieOptions{ Expires = DateTime.UtcNow.AddMinutes(10), Secure=true, IsEssential=true});
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string returnUrl = null)
    {
        
        returnUrl ??= Url.Content("~/");
        
        var invitation = Request.Cookies["AccountInvitationCode"]??"";
        var invite = await _userManagementService.GetValidInvite(invitation);
        
        if (string.IsNullOrWhiteSpace(invite?.invitation_identifier)) {
            _logger.LogWarning($"Invalid invitation passed and attempted to create user with: {invitation}");
            TempData["error_message"] = "Account could not be created";
            return RedirectToPage("./Login");
        }
        if (ModelState.IsValid) 
        {
            var user = CreateUser();
            user.EmailConfirmed = true;

            await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                
                _logger.LogInformation($"User {Input.Email} created a new account with password.");
                
                
                // Add them to the user role and any claims that were passed in the invitation
                await _userManager.AddToRoleAsync(user, Roles.User);

                await _signInManager.SignInAsync(user, isPersistent: false);
                TempData["success_message"] = "Account successfully created";

                await _userManagementService.RevokeInvite(invitation);
                
                return LocalRedirect(returnUrl);
                
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }

    private ApplicationUser CreateUser()
    {
        try
        {
            return Activator.CreateInstance<ApplicationUser>();
        }
        catch
        {
            throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
        }
    }

    private IUserEmailStore<ApplicationUser> GetEmailStore()
    {
        if (!_userManager.SupportsUserEmail)
        {
            throw new NotSupportedException("The default UI requires a user store with email support.");
        }
        return (IUserEmailStore<ApplicationUser>)_userStore;
    }
}