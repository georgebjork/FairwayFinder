using FairwayFinder.Data;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services;

public class CourseService : ICourseService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public CourseService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<CourseSearchResult>> SearchCoursesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<CourseSearchResult>();
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Courses
            .Where(c => !c.IsDeleted && c.CourseName.ToLower().Contains(query.ToLower()))
            .OrderBy(c => c.CourseName)
            .Take(20)
            .Select(c => new CourseSearchResult
            {
                CourseId = c.CourseId,
                CourseName = c.CourseName,
                Address = c.Address
            })
            .ToListAsync();
    }

    public async Task<List<TeeboxOption>> GetTeeboxesAsync(long courseId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Teeboxes
            .Where(t => t.CourseId == courseId && !t.IsDeleted)
            .OrderBy(t => t.YardageTotal)
            .Select(t => new TeeboxOption
            {
                TeeboxId = t.TeeboxId,
                TeeboxName = t.TeeboxName,
                Par = t.Par,
                Rating = t.Rating,
                Slope = t.Slope,
                YardageTotal = t.YardageTotal,
                IsNineHole = t.IsNineHole,
                IsWomens = t.IsWomens
            })
            .ToListAsync();
    }

    public async Task<List<HoleInfo>> GetHolesAsync(long teeboxId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Holes
            .Where(h => h.TeeboxId == teeboxId && !h.IsDeleted)
            .OrderBy(h => h.HoleNumber)
            .Select(h => new HoleInfo
            {
                HoleId = h.HoleId,
                HoleNumber = (int)h.HoleNumber,
                Par = (int)h.Par,
                Yardage = (int)h.Yardage,
                Handicap = (int)h.Handicap
            })
            .ToListAsync();
    }
}
