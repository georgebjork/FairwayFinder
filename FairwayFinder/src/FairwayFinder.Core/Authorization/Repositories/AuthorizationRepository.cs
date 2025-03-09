using Dapper;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Authorization.Repositories;

public interface IAuthorizationRepository : IBaseRepository
{
    public Task<List<long>> GetScorecardsByUserId(string userId);
}

public class AuthorizationRepository(IConfiguration configuration, ILogger<IAuthorizationRepository> logger) : BasePgRepository(configuration), IAuthorizationRepository
{
    private readonly ILogger<IAuthorizationRepository> _logger = logger;
    
    public async Task<List<long>> GetScorecardsByUserId(string userId)
    {
        var sql = @"SELECT round_id FROM public.round WHERE user_id = @userId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<long>(sql, new { userId });
        return rv.ToList();
    }
}