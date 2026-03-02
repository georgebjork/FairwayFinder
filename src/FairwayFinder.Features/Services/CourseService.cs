using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
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

    // ── Existing read methods ────────────────────────────────

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
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Yardage = h.Yardage,
                Handicap = h.Handicap
            })
            .ToListAsync();
    }

    // ── Course CRUD ──────────────────────────────────────────

    public async Task<List<CourseListItem>> GetAllCoursesAsync()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        return await dbContext.Courses
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.CourseName)
            .Select(c => new CourseListItem
            {
                CourseId = c.CourseId,
                CourseName = c.CourseName,
                Address = c.Address,
                PhoneNumber = c.PhoneNumber,
                TeeboxCount = dbContext.Teeboxes.Count(t => t.CourseId == c.CourseId && !t.IsDeleted)
            })
            .ToListAsync();
    }

    public async Task<CourseDetailResponse?> GetCourseDetailAsync(long courseId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var course = await dbContext.Courses
            .Where(c => c.CourseId == courseId && !c.IsDeleted)
            .FirstOrDefaultAsync();

        if (course is null) return null;

        var teeboxes = await dbContext.Teeboxes
            .Where(t => t.CourseId == courseId && !t.IsDeleted)
            .OrderBy(t => t.YardageTotal)
            .Select(t => new TeeboxSummary
            {
                TeeboxId = t.TeeboxId,
                TeeboxName = t.TeeboxName,
                Par = t.Par,
                Rating = t.Rating,
                Slope = t.Slope,
                YardageOut = t.YardageOut,
                YardageIn = t.YardageIn,
                YardageTotal = t.YardageTotal,
                IsNineHole = t.IsNineHole,
                IsWomens = t.IsWomens
            })
            .ToListAsync();

        return new CourseDetailResponse
        {
            CourseId = course.CourseId,
            CourseName = course.CourseName,
            Address = course.Address,
            PhoneNumber = course.PhoneNumber,
            Teeboxes = teeboxes
        };
    }

    public async Task<long> CreateCourseAsync(SaveCourseRequest request, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var course = new Course
        {
            CourseName = request.CourseName,
            Address = request.Address,
            PhoneNumber = request.PhoneNumber,
            CreatedBy = userId,
            CreatedOn = now,
            UpdatedBy = userId,
            UpdatedOn = now,
            IsDeleted = false
        };

        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync();

        return course.CourseId;
    }

    public async Task<bool> UpdateCourseAsync(SaveCourseRequest request, string userId)
    {
        if (request.CourseId is null) return false;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var course = await dbContext.Courses
            .Where(c => c.CourseId == request.CourseId && !c.IsDeleted)
            .FirstOrDefaultAsync();

        if (course is null) return false;

        course.CourseName = request.CourseName;
        course.Address = request.Address;
        course.PhoneNumber = request.PhoneNumber;
        course.UpdatedBy = userId;
        course.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteCourseAsync(long courseId, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        var course = await dbContext.Courses
            .Where(c => c.CourseId == courseId && !c.IsDeleted)
            .FirstOrDefaultAsync();

        if (course is null) return false;

        // Soft-delete the course
        course.IsDeleted = true;
        course.UpdatedBy = userId;
        course.UpdatedOn = now;

        // Soft-delete all teeboxes for this course
        var teeboxes = await dbContext.Teeboxes
            .Where(t => t.CourseId == courseId && !t.IsDeleted)
            .ToListAsync();

        foreach (var teebox in teeboxes)
        {
            teebox.IsDeleted = true;
            teebox.UpdatedBy = userId;
            teebox.UpdatedOn = now;
        }

        // Soft-delete all holes for this course
        var holes = await dbContext.Holes
            .Where(h => h.CourseId == courseId && !h.IsDeleted)
            .ToListAsync();

        foreach (var hole in holes)
        {
            hole.IsDeleted = true;
            hole.UpdatedBy = userId;
            hole.UpdatedOn = now;
        }

        await dbContext.SaveChangesAsync();
        return true;
    }

    // ── Teebox CRUD ──────────────────────────────────────────

    public async Task<TeeboxDetailResponse?> GetTeeboxDetailAsync(long teeboxId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var teebox = await dbContext.Teeboxes
            .Where(t => t.TeeboxId == teeboxId && !t.IsDeleted)
            .FirstOrDefaultAsync();

        if (teebox is null) return null;

        var holes = await dbContext.Holes
            .Where(h => h.TeeboxId == teeboxId && !h.IsDeleted)
            .OrderBy(h => h.HoleNumber)
            .Select(h => new HoleInfo
            {
                HoleId = h.HoleId,
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Yardage = h.Yardage,
                Handicap = h.Handicap
            })
            .ToListAsync();

        return new TeeboxDetailResponse
        {
            TeeboxId = teebox.TeeboxId,
            CourseId = teebox.CourseId,
            TeeboxName = teebox.TeeboxName,
            Rating = teebox.Rating,
            Slope = teebox.Slope,
            IsNineHole = teebox.IsNineHole,
            IsWomens = teebox.IsWomens,
            Holes = holes
        };
    }

    public async Task<long> CreateTeeboxAsync(SaveTeeboxRequest request, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        // Compute aggregate values from hole data
        var par = request.Holes.Sum(h => h.Par);
        var yardageOut = request.Holes.Where(h => h.HoleNumber <= 9).Sum(h => h.Yardage);
        var yardageIn = request.Holes.Where(h => h.HoleNumber > 9).Sum(h => h.Yardage);
        var yardageTotal = request.Holes.Sum(h => h.Yardage);

        var teebox = new Teebox
        {
            CourseId = request.CourseId,
            TeeboxName = request.TeeboxName,
            Par = par,
            Rating = request.Rating,
            Slope = request.Slope,
            YardageOut = yardageOut,
            YardageIn = yardageIn,
            YardageTotal = yardageTotal,
            IsNineHole = request.IsNineHole,
            IsWomens = request.IsWomens,
            CreatedBy = userId,
            CreatedOn = now,
            UpdatedBy = userId,
            UpdatedOn = now,
            IsDeleted = false
        };

        dbContext.Teeboxes.Add(teebox);
        await dbContext.SaveChangesAsync();

        // Create holes
        var holes = request.Holes.Select(h => new Hole
        {
            TeeboxId = teebox.TeeboxId,
            CourseId = request.CourseId,
            HoleNumber = h.HoleNumber,
            Par = h.Par,
            Yardage = h.Yardage,
            Handicap = h.Handicap,
            CreatedBy = userId,
            CreatedOn = now,
            UpdatedBy = userId,
            UpdatedOn = now,
            IsDeleted = false
        }).ToList();

        dbContext.Holes.AddRange(holes);
        await dbContext.SaveChangesAsync();

        return teebox.TeeboxId;
    }

    public async Task<bool> UpdateTeeboxAsync(SaveTeeboxRequest request, string userId)
    {
        if (request.TeeboxId is null) return false;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        var teebox = await dbContext.Teeboxes
            .Where(t => t.TeeboxId == request.TeeboxId && !t.IsDeleted)
            .FirstOrDefaultAsync();

        if (teebox is null) return false;

        // Compute aggregate values from hole data
        var par = request.Holes.Sum(h => h.Par);
        var yardageOut = request.Holes.Where(h => h.HoleNumber <= 9).Sum(h => h.Yardage);
        var yardageIn = request.Holes.Where(h => h.HoleNumber > 9).Sum(h => h.Yardage);
        var yardageTotal = request.Holes.Sum(h => h.Yardage);

        // Update teebox
        teebox.TeeboxName = request.TeeboxName;
        teebox.Par = par;
        teebox.Rating = request.Rating;
        teebox.Slope = request.Slope;
        teebox.YardageOut = yardageOut;
        teebox.YardageIn = yardageIn;
        teebox.YardageTotal = yardageTotal;
        teebox.IsNineHole = request.IsNineHole;
        teebox.IsWomens = request.IsWomens;
        teebox.UpdatedBy = userId;
        teebox.UpdatedOn = now;

        // Soft-delete existing holes and create new ones
        var existingHoles = await dbContext.Holes
            .Where(h => h.TeeboxId == request.TeeboxId && !h.IsDeleted)
            .ToListAsync();

        foreach (var hole in existingHoles)
        {
            hole.IsDeleted = true;
            hole.UpdatedBy = userId;
            hole.UpdatedOn = now;
        }

        // Create new holes
        var newHoles = request.Holes.Select(h => new Hole
        {
            TeeboxId = teebox.TeeboxId,
            CourseId = request.CourseId,
            HoleNumber = h.HoleNumber,
            Par = h.Par,
            Yardage = h.Yardage,
            Handicap = h.Handicap,
            CreatedBy = userId,
            CreatedOn = now,
            UpdatedBy = userId,
            UpdatedOn = now,
            IsDeleted = false
        }).ToList();

        dbContext.Holes.AddRange(newHoles);
        await dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteTeeboxAsync(long teeboxId, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        var teebox = await dbContext.Teeboxes
            .Where(t => t.TeeboxId == teeboxId && !t.IsDeleted)
            .FirstOrDefaultAsync();

        if (teebox is null) return false;

        // Soft-delete the teebox
        teebox.IsDeleted = true;
        teebox.UpdatedBy = userId;
        teebox.UpdatedOn = now;

        // Soft-delete all holes for this teebox
        var holes = await dbContext.Holes
            .Where(h => h.TeeboxId == teeboxId && !h.IsDeleted)
            .ToListAsync();

        foreach (var hole in holes)
        {
            hole.IsDeleted = true;
            hole.UpdatedBy = userId;
            hole.UpdatedOn = now;
        }

        await dbContext.SaveChangesAsync();
        return true;
    }
}
