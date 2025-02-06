using Dapper;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.UserManagement.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.UserManagement.Respositories;

public interface IUserManagementRepository : IBaseRepository
{
    public Task<List<ApplicationUser>> GetAllUsers();
    public Task<List<ApplicationUser>> GetAllConfirmedUsers();
    public Task<List<ApplicationUser>> GetAllPendingUsers();
    public Task<List<UserInvitation>> GetAllInvitedUsers();
    public Task<List<string>> FindAllSimilarUsernames(string user_name);
    Task<UserInvitation?> GetInvite(string inviteId);
    Task<string?> GetUserIdByUsername(string username);
}

public class UserManagementRepository(IConfiguration configuration, ILogger<UserManagementRepository> logger) : BasePgRepository(configuration), IUserManagementRepository
{
    public async Task<List<ApplicationUser>> GetAllUsers()
    {
        var sql = @"SELECT * FROM public.""AspNetUsers""";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<ApplicationUser>(sql);
        return rv.ToList();
    }

    public async Task<List<ApplicationUser>> GetAllConfirmedUsers()
    {
        var sql = @"SELECT * FROM public.""AspNetUsers"" WHERE ""EmailConfirmed"" = true";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<ApplicationUser>(sql);
        return rv.ToList();
    }

    public async Task<List<ApplicationUser>> GetAllPendingUsers()
    {
        var sql = @"SELECT * FROM public.""AspNetUsers"" WHERE ""EmailConfirmed"" = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<ApplicationUser>(sql);
        return rv.ToList();
    }

    public async Task<List<UserInvitation>> GetAllInvitedUsers()
    {
        var sql = "SELECT * FROM user_invitation WHERE is_deleted = false";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<UserInvitation>(sql);
        return rv.ToList();
    }

    public async Task<List<string>> FindAllSimilarUsernames(string userName)
    {
        var sql = "SELECT \"UserName\"FROM public.\"AspNetUsers\"WHERE \"UserName\" LIKE @userName || '%';";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<string>(sql, new { userName });
        return rv.ToList();
    }

    public async Task<UserInvitation?> GetInvite(string inviteId)
    {
        var sql = "SELECT * FROM user_invitation WHERE is_deleted = false AND invitation_identifier = @id";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<UserInvitation>(sql, new {id = inviteId});
        return rv;
    }

    public async Task<string?> GetUserIdByUsername(string username)
    {
        var sql = @"SELECT ""Id"" FROM public.""AspNetUsers"" WHERE ""UserName"" = @username";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<string>(sql, new { username });
        return rv;
    }
}