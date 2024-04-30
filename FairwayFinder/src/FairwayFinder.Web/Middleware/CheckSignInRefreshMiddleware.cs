using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Services.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Web.Middleware;

public class CheckSignInRefreshMiddleware(
    RequestDelegate next,
    IRoleRefreshService roleRefreshService,
    IUsernameRetriever usernameRetriever)
{
    public async Task InvokeAsync(HttpContext context, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        var userId = usernameRetriever.UserId;
        if (await roleRefreshService.CheckRefreshFlag(userId))
        {
            var user = await userManager.FindByIdAsync(userId);
            
            // Check if user was found
            if (user == null)
            {
                // No user was returned. It is possible the user was deleted, so let's sign them out just in case.
                await signInManager.SignOutAsync();
            }
            else
            {
                // Check if the user is currently locked out
                if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
                {
                    // They are locked out. Sign them out.
                    await signInManager.SignOutAsync();
                    
                    if (context.Request.Headers.ContainsKey("HX-Request")) // Check if it's an HTMX request
                    {
                        context.Response.Headers["HX-Redirect"] = "/login"; // Direct HTMX to handle the redirect
                    }
                    else
                    {
                        context.Response.Redirect("/login"); // Standard redirect for non-HTMX requests
                    }
                }
                else
                {
                    // User is not locked out, refresh their signin
                    await signInManager.RefreshSignInAsync(user);
                }
            }

            // Regardless of the user's state, remove the refresh flag
            await roleRefreshService.RemoveRefreshFlag(userId);
        }
        await next(context);
    }
}
