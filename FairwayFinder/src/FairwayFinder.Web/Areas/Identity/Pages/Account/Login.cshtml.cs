using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Settings.Authorization;
using FairwayFinder.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FairwayFinder.Web.Areas.Identity.Pages.Account;
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(SignInManager<ApplicationUser> signInManager, ILogger<LoginModel> logger, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _logger = logger;
        _userManager = userManager;
    }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public IList<AuthenticationScheme> ExternalLogins { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public string? ReturnUrl { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string ErrorMessage { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InputModel
    {
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
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

        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (ModelState.IsValid)
        {
            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            
            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }
            
            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError(string.Empty, "You're email is not yet confirmed.");
                return Page();
            }
            
            var result = await _signInManager.PasswordSignInAsync(user.UserName!, Input.Password, Input.RememberMe, lockoutOnFailure: false);
            
            if (result.Succeeded)
            {
                await AddOrUpdateNameClaims(user);
                await _signInManager.RefreshSignInAsync(user);
                
                _logger.LogInformation("User logged in.");
                TempData["success_message"] = "Successfully logged in.";
                return LocalRedirect(returnUrl);
            }
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }

    private async Task AddOrUpdateNameClaims(ApplicationUser user)
    {
        var currentClaims = await _userManager.GetClaimsAsync(user);
        
        var updatedClaims = new List<Claim>
        {
            new (CustomClaims.FirstName, user!.FirstName ?? "unknown"),
            new (CustomClaims.LastName, user!.LastName ?? "unknown")
        };
        
        foreach (var claim in updatedClaims)
        {
            var existingClaim = currentClaims.FirstOrDefault(c => c.Type == claim.Type);
            if (existingClaim != null)
            {
                // Check if the existing claim needs an update
                if (existingClaim.Value != claim.Value)
                {
                    await _userManager.ReplaceClaimAsync(user, existingClaim, claim);
                }
            }
            else
            {
                // Add the claim if it doesn't exist
                await _userManager.AddClaimAsync(user, claim);
            }
        }
    }
}