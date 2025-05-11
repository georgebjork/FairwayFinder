using Dapper;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FairwayFinder.Core.Repositories;

public class DocumentRepository(IConfiguration configuration) : BasePgRepository(configuration), IDocumentRepository
{
    public async Task<List<ProfileDocument>> GetUserProfilePictureRecordsAsync(string userId)
    {
        await using var conn = await GetNewOpenConnection();
        var sql = "SELECT * FROM profile_document WHERE user_id = @userId AND is_deleted = false";
        var rv = await conn.QueryAsync<ProfileDocument>(sql, new {userId});
        return rv.ToList();
    }

    public async Task<ProfileDocument?> GetUserProfilePictureRecordAsync(string userId)
    {
        await using var conn = await GetNewOpenConnection();
        var sql = "SELECT * FROM profile_document WHERE user_id = @userId AND is_deleted = false ORDER BY created_on DESC LIMIT 1";
        var rv = await conn.QueryFirstOrDefaultAsync<ProfileDocument>(sql, new {userId});
        return rv;
    }

    public async Task<bool> DeleteProfilePictureAsync(string documentId)
    {
        await using var conn = await GetNewOpenConnection();
        var sql = "UPDATE profile_document SET is_deleted = true WHERE document_id = @documentId";
        var rv = await conn.ExecuteAsync(sql, new { documentId });
        return rv == 1;
    }
}