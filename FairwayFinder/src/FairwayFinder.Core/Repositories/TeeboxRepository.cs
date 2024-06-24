using Dapper;
using FairwayFinder.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Repositories;

public interface ITeeboxRepository : IBaseRepository
{
    Task<List<Teebox>> GetAllTeeboxes();
    Task<List<Teebox>> GetTeeboxesByCourseId(int courseId);
    Task<Teebox?> GetTeeboxById(int teeboxId);
}

public class TeeboxRepository(IConfiguration configuration, ILogger<TeeboxRepository> logger) : BasePgRepository(configuration, logger), ITeeboxRepository
{
    public async Task<List<Teebox>> GetAllTeeboxes()
    {
        var sql = "SELECT * FROM public.teebox";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Teebox>(sql);
        return rv.ToList();
    }

    public async Task<List<Teebox>> GetTeeboxesByCourseId(int courseId)
    {
        var sql = "SELECT * FROM public.teebox WHERE course_id = @courseId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryAsync<Teebox>(sql, new { courseId });
        return rv.ToList();
    }

    public async Task<Teebox?> GetTeeboxById(int teeboxId)
    {
        var sql = "SELECT * FROM public.teebox WHERE teebox_id = @teeboxId";
        await using var conn = await GetNewOpenConnection();
        var rv = await conn.QueryFirstOrDefaultAsync<Teebox>(sql, new { teeboxId });
        return rv;
    }
}