using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Identity.Settings;
using FairwayFinder.Core.UserManagement.Services;
using FairwayFinder.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FairwayFinder.Web.Areas.Identity.Pages.Account;

public class Register : PageModel
{
    private readonly IUserManagementService _userManagementService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<Register> _logger;

    public Register(IUserManagementService userManagementService, UserManager<ApplicationUser> userManager, ILogger<Register> logger)
    {
        _userManagementService = userManagementService;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();
    
    public string? ReturnUrl { get; set; }
    
    public class InputModel
    {
        public string? InvitationCode{ get; set; } 

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

       
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = "";
        
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = "";
        
        [Required]
        [MaxLength(250, ErrorMessage = "Maximum length is 250 characters.")]
        [RegularExpression(@"^[a-zA-Z\s'-]+$", ErrorMessage = "First Name can only contain letters, spaces, apostrophes, and hyphens.")]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }  = "";
    
        [Required]
        [MaxLength(250, ErrorMessage = "Maximum length is 250 characters.")]
        [RegularExpression(@"^[a-zA-Z\s'-]+$", ErrorMessage = "Last Name can only contain letters, spaces, apostrophes, and hyphens.")]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }  = "";
        
        
    }
    
    public async Task<IActionResult> OnGetAsync(string? invitation, string? returnUrl = null)
    {
        // If no invitation, then return a blank sign in page
        if (invitation is null) {
            Input = new InputModel();
            return Page();
        }
        
        // Saved for future email invite
        var invite = await _userManagementService.GetValidInvite(invitation);
        if (string.IsNullOrWhiteSpace(invite?.invitation_identifier)) {
            return RedirectToPage("./Login");
        }
        Input = new InputModel { InvitationCode = invitation, Email = invite.sent_to_email };
        ReturnUrl = returnUrl;
        Response.Cookies.Append("AccountInvitationCode", invitation, new CookieOptions{ Expires = DateTime.UtcNow.AddMinutes(10), Secure=true, IsEssential=true});
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        
        if (!ModelState.IsValid) return Page();

        // Get invite code if exists
        var invitation_code = Request.Cookies["AccountInvitationCode"] ?? "";
        
        var user = CreateUser();
        
        user.EmailConfirmed = false;
        user.Email = Input.Email;
        user.FirstName = Input.FirstName;
        user.LastName = Input.LastName;
        user.UserName = await _userManagementService.GenerateUserName(user.FirstName, user.LastName);
        
        var result = await _userManager.CreateAsync(user, Input.Password);
        
        if (result.Succeeded)
        {
            _logger.LogInformation($"User {Input.Email} created a new account with password.");
            
            // Add new user to default role of user
            await _userManager.AddToRoleAsync(user, Roles.User);
            
            // Add our claims for http context
            var claims = new List<Claim>
            {
                new (CustomClaims.FirstName, user!.FirstName ?? "unknown"),
                new (CustomClaims.LastName, user!.LastName ?? "unknown")
            };
            await _userManager.AddClaimsAsync(user, claims);
            
            TempData["success_message"] = "Account successfully created. A confirmation email has been sent to your email address.";
                
            await _userManagementService.RevokeInvite(invitation_code);
            // Send a confirmation email. 
            //await _userManagementService.CreateAndSendEmailConfirmation(Input.Email, $"{Request.Scheme}://{Request.Host.Value}");
                
            return LocalRedirect(returnUrl);
                
        }
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
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
            throw new InvalidOperationException(
                $"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml"
            );
        }
    }
    
}