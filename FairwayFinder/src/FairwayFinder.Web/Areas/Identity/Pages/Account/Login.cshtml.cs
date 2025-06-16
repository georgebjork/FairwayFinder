using System.ComponentModel.DataAnnotations;
using FairwayFinder.Core.Identity;
using FairwayFinder.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FairwayFinder.Web.Areas.Identity.Pages.Account;

public class Login : PageModel
{
    
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<Login> _logger;

    public Login(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<Login> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }
    
    [BindProperty]
    public InputModel Input { get; set; } = new();
    
    [TempData]
    public string ErrorMessage { get; set; } = string.Empty;
    
    public string? ReturnUrl { get; set; }
    
    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";
        
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
        
        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }


    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        returnUrl ??= Url.Content("~/");

        // Clear the existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        
        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }
            
            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError(string.Empty, "Your email is not yet confirmed.");
                return Page();
            }
            
            var result = await _signInManager.PasswordSignInAsync(user.UserName!, Input.Password, Input.RememberMe, lockoutOnFailure: false);
            
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                
                TempData["success_message"] = "Account Successfully Signed In";
                
                _logger.LogInformation("User {0} logged in.", user.Email ?? "unknown");
                return LocalRedirect(returnUrl);
            }
            
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                
                ModelState.AddModelError(string.Empty, "Your account has been locked.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }

            return Page();
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }
}