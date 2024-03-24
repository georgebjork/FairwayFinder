using Dapper;
using FairwayFinder.Core.Features.Admin.UserManagement.Models;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Admin.UserManagement;

public interface IUserRepository : IBaseRepository
{
    Task<List<ApplicationUser>> GetUsers();
    
    // User Invites
    Task<List<UserInvitation>> GetInvites();
    Task<UserInvitation?> GetInvite(string inviteId);
}

public class UserRepository(IConfiguration configuration, ILogger<UserRepository> logger) : BasePgRepository(configuration, logger), IUserRepository
{
    private readonly ILogger<UserRepository> logger = logger;


    public async Task<List<ApplicationUser>> GetUsers()
    {
        var sql = @"SELECT * FROM public.""AspNetUsers""";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<ApplicationUser>(sql);
        return rv.ToList();
    }

    public async Task<List<UserInvitation>> GetInvites()
    {
        var sql = "SELECT * FROM user_invitation WHERE is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<UserInvitation>(sql);
        return rv.ToList();
    }

    public async Task<UserInvitation?> GetInvite(string inviteId)
    {
        var sql = "SELECT * FROM user_invitation WHERE is_deleted = false AND invitation_identifier = @id";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<UserInvitation>(sql, new {id = inviteId});
        return rv;
    }
}