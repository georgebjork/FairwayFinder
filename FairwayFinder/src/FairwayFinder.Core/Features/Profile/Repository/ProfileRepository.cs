using Dapper;
using FairwayFinder.Core.Features.Profile.Models.QueryModel;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Profile;

public interface IProfileRepository : IBaseRepository
{
    Task<ProfileQueryModel?> GetProfileByEmail(string email);
    Task<ProfileQueryModel?> GetProfileByUserName(string userName);
    Task<bool> IsUserNameAvailable(string userName);
    Task<int> UpdateProfile(ProfileQueryModel model);
    Task<List<string>> FindSimilarHandles(string handle);
}

public class ProfileRepository(IConfiguration configuration, ILogger<ProfileRepository> logger) : BasePgRepository(configuration, logger), IProfileRepository
{
    public async Task<ProfileQueryModel?> GetProfileByEmail(string email)
    {
        var sql =
            "SELECT \"Id\", \"Email\", \"FirstName\", \"LastName\", \"UserName\"\n\t FROM public.\"AspNetUsers\" WHERE \"Email\" = @email";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<ProfileQueryModel>(sql, new { email });
        return rv;
    }

    public async Task<ProfileQueryModel?> GetProfileByUserName(string userName)
    {
        var sql =
            "SELECT \"Id\", \"Email\", \"FirstName\", \"LastName\", \"UserName\"\n\t FROM public.\"AspNetUsers\" WHERE \"UserName\" = @userName";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<ProfileQueryModel>(sql, new { userName });
        return rv;
    }

    public async Task<bool> IsUserNameAvailable(string userName)
    {
        var sql =
            "SELECT \"UserName\"\n\t FROM public.\"AspNetUsers\" WHERE \"UserName\" = @userName";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<string>(sql, new { userName });
        return string.IsNullOrEmpty(rv); // Return true if empty/null (available) or false (is not available)
    }

    public async Task<int> UpdateProfile(ProfileQueryModel model)
    {
        var sql = @"UPDATE public.""AspNetUsers""
	                SET ""FirstName""=@firstName, ""LastName""=@lastName, ""UserName""=@userName, ""UpdatedOn""=@date
                    WHERE ""Id"" = @id";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.ExecuteAsync(sql,
            new { firstName = model.FirstName, lastName = model.LastName, userName = model.UserName, id = model.Id, date = DateTime.UtcNow });
        return rv;
    }

    public async Task<List<string>> FindSimilarHandles(string userName)
    {
        var sql = "SELECT \"UserName\"FROM public.\"AspNetUsers\"WHERE \"UserName\" LIKE @userName || '%';";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<string>(sql, new { userName });
        return rv.ToList();
    }
}