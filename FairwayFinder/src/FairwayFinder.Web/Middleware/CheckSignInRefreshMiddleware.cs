using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Services.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Web.Middleware;

public class CheckSignInRefreshMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRoleRefreshService _roleRefreshService;
    private readonly IUsernameRetriever _usernameRetriever;

    public CheckSignInRefreshMiddleware(RequestDelegate next, IRoleRefreshService roleRefreshService, IUsernameRetriever usernameRetriever)
    {
        _next = next;
        _roleRefreshService = roleRefreshService;
        _usernameRetriever = usernameRetriever;
    }

    public async Task InvokeAsync(HttpContext context, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        var userId = _usernameRetriever.UserId;
        if (await _roleRefreshService.CheckRefreshFlag(userId))
        {
            var user = await userManager.FindByIdAsync(userId);

            if (user is not null)
            {
                await signInManager.RefreshSignInAsync(user);
                await _roleRefreshService.RemoveRefreshFlag(userId);
            }
        }
        
        await _next(context);
    }
}
