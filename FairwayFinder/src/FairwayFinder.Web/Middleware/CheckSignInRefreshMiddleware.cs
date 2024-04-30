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

            if (user is not null)
            {
                await signInManager.RefreshSignInAsync(user);
                await roleRefreshService.RemoveRefreshFlag(userId);
            }
        }
        await next(context);
    }
}
