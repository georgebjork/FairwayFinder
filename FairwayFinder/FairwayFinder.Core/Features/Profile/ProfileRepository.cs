using Dapper;
using FairwayFinder.Core.Features.Profile.Models.QueryModel;
using FairwayFinder.Core.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Profile;

public interface IProfileRepository : IBaseRepository
{
    Task<ProfileQueryModel?> GetProfileByEmail(string email);
    Task<bool> IsHandleAvailable(string handle);
}

public class ProfileRepository(IConfiguration configuration, ILogger<ProfileRepository> logger) : BasePgRepository(configuration, logger), IProfileRepository
{
    public async Task<ProfileQueryModel?> GetProfileByEmail(string email)
    {
        var sql =
            "SELECT \"Id\", \"Email\", \"FirstName\", \"LastName\", \"Handle\"\n\t FROM public.\"AspNetUsers\" WHERE \"Email\" = @email";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<ProfileQueryModel>(sql, new { email });
        return rv;
    }

    public async Task<bool> IsHandleAvailable(string handle)
    {
        var sql =
            "SELECT \"Handle\"\n\t FROM public.\"AspNetUsers\" WHERE \"Handle\" = @handle";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<string>(sql, new { handle });
        return string.IsNullOrEmpty(rv); // Return true if empty/null (available) or false (is not available)
    }
}