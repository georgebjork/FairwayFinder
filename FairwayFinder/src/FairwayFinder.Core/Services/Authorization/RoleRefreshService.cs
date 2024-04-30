using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Services.Authorization;

public interface IRoleRefreshService
{
    Task<bool> CheckRefreshFlag(string userId);
    Task SetRefreshFlag(string userId);   
    Task RemoveRefreshFlag(string userId);
}

public class RoleRefreshService(IDistributedCache cache, ILogger<RoleRefreshService> logger) : IRoleRefreshService
{
    
    private const string RoleRefreshKey = "Refresh_Role_";

    public async Task<bool> CheckRefreshFlag(string userId)
    {
        var result = await cache.GetStringAsync($"{RoleRefreshKey}{userId}");
        bool.TryParse(result, out var val);
        {
            return val;
        }
    }

    public async Task SetRefreshFlag(string userId)
    {
        try
        {
            await cache.SetStringAsync($"{RoleRefreshKey}{userId}", "true");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to set cache flag for userId {0}", userId);
        }
        
    }

    public async Task RemoveRefreshFlag(string userId)
    {
        try
        {
            await cache.RemoveAsync($"{RoleRefreshKey}{userId}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to remove cache flag for userId {0}", userId);
        }
    }
}