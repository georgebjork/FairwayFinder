using Dapper;
using FairwayFinder.Core.Features.Profile.Models.QueryModel;
using FairwayFinder.Core.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Profile;

public interface IProfileRepository : IBaseRepository
{
    Task<ProfileQueryModel?> GetProfileByEmail(string email);
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
}