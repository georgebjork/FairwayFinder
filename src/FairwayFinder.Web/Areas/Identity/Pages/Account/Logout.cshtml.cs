using FairwayFinder.Core.Identity;
using FairwayFinder.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FairwayFinder.Web.Areas.Identity.Pages.Account;

public class Logout : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<Logout> _logger;

    public Logout(SignInManager<ApplicationUser> signInManager, ILogger<Logout> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    public async Task<IActionResult> OnGet(string? returnUrl = null)
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation($"User logged out.");
        Response.Headers["HX-Redirect"] = "/Identity/Account/Login";
        return Redirect("/");
    }
}