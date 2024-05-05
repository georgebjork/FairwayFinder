using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Identity.Authorization;

public interface IUserRefreshService
{
    Task<bool> CheckRefreshFlag(string userId);
    Task SetRefreshFlag(string userId);   
    Task RemoveRefreshFlag(string userId);
}

public class UserRefreshService(IDistributedCache cache, ILogger<UserRefreshService> logger) : IUserRefreshService
{
    
    private const string RefreshKey = "Refresh_User_";

    public async Task<bool> CheckRefreshFlag(string userId)
    {
        var result = await cache.GetStringAsync($"{RefreshKey}{userId}");
        bool.TryParse(result, out var val);
        {
            return val;
        }
    }

    public async Task SetRefreshFlag(string userId)
    {
        try
        {
            await cache.SetStringAsync($"{RefreshKey}{userId}", "true");
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
            await cache.RemoveAsync($"{RefreshKey}{userId}");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to remove cache flag for userId {0}", userId);
        }
    }
}