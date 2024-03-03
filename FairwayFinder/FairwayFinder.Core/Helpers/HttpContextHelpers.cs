using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace FairwayFinder.Core.Helpers;

public static class HttpContextHelpers
{
    public static string CurrentUserName(this IHttpContextAccessor context) => context.HttpContext?.User.Identity?.Name ?? "unknown";
    public static string CurrentUserName(this HttpContext context) {
        return context.User.Identity?.Name ?? "unknown";
    }
    
    public static string CurrentUserId(this IHttpContextAccessor context) => 
        context.HttpContext?.User?.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value ?? "unknown";

    public static string CurrentUserId(this HttpContext context) {
        return context.User.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value ?? "unknown";
    }
}