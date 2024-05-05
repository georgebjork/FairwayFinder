using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using FairwayFinder.Identity.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Web.Middleware;

public class RefreshUserMiddleware(
    RequestDelegate next,
    IUserRefreshService userRefreshService,
    IUsernameRetriever usernameRetriever)
{
    public async Task InvokeAsync(HttpContext context, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        var userId = usernameRetriever.UserId;
        if (await userRefreshService.CheckRefreshFlag(userId))
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
                    
                    context.Response.StatusCode = 302; // Explicitly set the status code for clarity
                    await context.Response.CompleteAsync();
                    return; // Short-circuit the pipeline here
                }

                // User is not locked out, refresh their signin
                await signInManager.RefreshSignInAsync(user);
            }

            // Regardless of the user's state, remove the refresh flag
            await userRefreshService.RemoveRefreshFlag(userId);
        }
        await next(context);
    }
}
