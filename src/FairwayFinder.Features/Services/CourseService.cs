using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
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
                Address = c.Address,
                City = c.City,
                State = c.State
            })
            .ToListAsync();
    }

    public async Task<List<TeeboxOption>> GetTeeboxesAsync(long courseId, PreferredTees? filterByPreferredTees = null)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var query = dbContext.Teeboxes
            .Where(t => t.CourseId == courseId && !t.IsDeleted && t.ArchivedOn == null);

        if (filterByPreferredTees is not null)
        {
            var wantsWomens = filterByPreferredTees == PreferredTees.Womens;
            query = query.Where(t => t.IsWomens == wantsWomens);
        }

        return await query
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
                ClubName = c.ClubName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Country = c.Country,
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
            .OrderBy(t => t.ArchivedOn != null) // active tees first, archived versions after
            .ThenBy(t => t.YardageTotal)
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
                IsWomens = t.IsWomens,
                TeeboxGroupId = t.TeeboxGroupId,
                ArchivedOn = t.ArchivedOn
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

    public async Task<List<HoleInfo>> GetParHandicapTemplateAsync(long courseId, bool isWomens, bool isNineHole)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Prefer a tee of the same gender + hole count; fall back to any active tee of the same hole count.
        var template = await dbContext.Teeboxes
            .Where(t => t.CourseId == courseId && !t.IsDeleted && t.ArchivedOn == null
                        && t.IsNineHole == isNineHole && t.IsWomens == isWomens)
            .OrderByDescending(t => t.CreatedOn)
            .FirstOrDefaultAsync()
            ?? await dbContext.Teeboxes
                .Where(t => t.CourseId == courseId && !t.IsDeleted && t.ArchivedOn == null
                            && t.IsNineHole == isNineHole)
                .OrderByDescending(t => t.CreatedOn)
                .FirstOrDefaultAsync();

        if (template is null) return new List<HoleInfo>();

        return await dbContext.Holes
            .Where(h => h.TeeboxId == template.TeeboxId && !h.IsDeleted)
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

        // A brand-new tee is its own lineage; group id defaults to its own id.
        teebox.TeeboxGroupId = teebox.TeeboxId;

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

    public async Task<bool> UpdateTeeboxAsync(SaveTeeboxRequest request, string userId, bool cascadeToSameGender = false)
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

        var existingHoles = await dbContext.Holes
            .Where(h => h.TeeboxId == request.TeeboxId && !h.IsDeleted)
            .ToListAsync();

        var existingByNumber = existingHoles.ToDictionary(h => h.HoleNumber);
        var requestHoleNumbers = request.Holes.Select(h => h.HoleNumber).ToHashSet();

        foreach (var requestHole in request.Holes)
        {
            if (existingByNumber.TryGetValue(requestHole.HoleNumber, out var existing))
            {
                existing.Par = requestHole.Par;
                existing.Yardage = requestHole.Yardage;
                existing.Handicap = requestHole.Handicap;
                existing.UpdatedBy = userId;
                existing.UpdatedOn = now;
            }
            else
            {
                dbContext.Holes.Add(new Hole
                {
                    TeeboxId = teebox.TeeboxId,
                    CourseId = request.CourseId,
                    HoleNumber = requestHole.HoleNumber,
                    Par = requestHole.Par,
                    Yardage = requestHole.Yardage,
                    Handicap = requestHole.Handicap,
                    CreatedBy = userId,
                    CreatedOn = now,
                    UpdatedBy = userId,
                    UpdatedOn = now,
                    IsDeleted = false
                });
            }
        }

        foreach (var existing in existingHoles.Where(h => !requestHoleNumbers.Contains(h.HoleNumber)))
        {
            existing.IsDeleted = true;
            existing.UpdatedBy = userId;
            existing.UpdatedOn = now;
        }

        // Cascade par + handicap to the other active same-gender, same-hole-count tees (in place).
        if (cascadeToSameGender)
        {
            var siblings = await GetActiveSiblingTeesAsync(
                dbContext, request.CourseId, teebox.TeeboxId, request.IsWomens, request.IsNineHole);
            ApplyCascadeInPlace(siblings, BuildParHandicapMap(request.Holes), userId, now);
        }

        await dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<long> CreateTeeboxVersionAsync(SaveTeeboxRequest request, string userId, bool cascadeToSameGender = false)
    {
        if (request.TeeboxId is null) return 0;

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var now = DateOnly.FromDateTime(DateTime.UtcNow);

        // Source must be an active (non-archived, non-deleted) tee.
        var source = await dbContext.Teeboxes
            .Where(t => t.TeeboxId == request.TeeboxId && !t.IsDeleted && t.ArchivedOn == null)
            .FirstOrDefaultAsync();

        if (source is null) return 0;

        // Capture the sibling tees to cascade to BEFORE we add the new primary version, so the
        // freshly-created versions are not themselves picked up and re-versioned.
        var siblings = cascadeToSameGender
            ? await GetActiveSiblingTeesAsync(dbContext, source.CourseId, source.TeeboxId, request.IsWomens, request.IsNineHole)
            : new List<(Teebox Tee, List<Hole> Holes)>();

        // Compute aggregate values from hole data
        var par = request.Holes.Sum(h => h.Par);
        var yardageOut = request.Holes.Where(h => h.HoleNumber <= 9).Sum(h => h.Yardage);
        var yardageIn = request.Holes.Where(h => h.HoleNumber > 9).Sum(h => h.Yardage);
        var yardageTotal = request.Holes.Sum(h => h.Yardage);

        // Insert the new version, inheriting the source's lineage so stats treat them as one tee.
        var newTeebox = new Teebox
        {
            CourseId = source.CourseId,
            TeeboxName = request.TeeboxName,
            Par = par,
            Rating = request.Rating,
            Slope = request.Slope,
            YardageOut = yardageOut,
            YardageIn = yardageIn,
            YardageTotal = yardageTotal,
            IsNineHole = request.IsNineHole,
            IsWomens = request.IsWomens,
            TeeboxGroupId = source.TeeboxGroupId,
            ArchivedOn = null,
            ArchivedBy = null,
            CreatedBy = userId,
            CreatedOn = now,
            UpdatedBy = userId,
            UpdatedOn = now,
            IsDeleted = false
        };

        dbContext.Teeboxes.Add(newTeebox);
        await dbContext.SaveChangesAsync();

        var holes = request.Holes.Select(h => new Hole
        {
            TeeboxId = newTeebox.TeeboxId,
            CourseId = source.CourseId,
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

        // Archive the source. Leave its holes and IsDeleted untouched so historical rounds
        // still resolve their original rating/slope/handicaps.
        source.ArchivedOn = now;
        source.ArchivedBy = userId;
        source.UpdatedBy = userId;
        source.UpdatedOn = now;

        await dbContext.SaveChangesAsync();

        // Cascade to siblings: version each one too, syncing par + handicap while keeping its own
        // rating/slope/yardage.
        if (siblings.Count > 0)
        {
            var map = BuildParHandicapMap(request.Holes);
            foreach (var (siblingTee, siblingHoles) in siblings)
            {
                await CreateSiblingVersionAsync(dbContext, siblingTee, siblingHoles, map, userId, now);
            }
            await dbContext.SaveChangesAsync();
        }

        return newTeebox.TeeboxId;
    }

    // ── Cascade helpers ──────────────────────────────────────

    private static Dictionary<int, (int Par, int Handicap)> BuildParHandicapMap(IEnumerable<HoleEntry> holes)
        => holes.ToDictionary(h => h.HoleNumber, h => (h.Par, h.Handicap));

    /// <summary>
    /// Loads the active (non-deleted, non-archived) tees on the course that share the given gender
    /// and hole count, excluding the edited tee, together with each one's current holes.
    /// </summary>
    private async Task<List<(Teebox Tee, List<Hole> Holes)>> GetActiveSiblingTeesAsync(
        ApplicationDbContext dbContext, long courseId, long excludeTeeboxId, bool isWomens, bool isNineHole)
    {
        var tees = await dbContext.Teeboxes
            .Where(t => t.CourseId == courseId && t.TeeboxId != excludeTeeboxId
                        && !t.IsDeleted && t.ArchivedOn == null
                        && t.IsWomens == isWomens && t.IsNineHole == isNineHole)
            .ToListAsync();

        if (tees.Count == 0) return new List<(Teebox, List<Hole>)>();

        var ids = tees.Select(t => t.TeeboxId).ToList();
        var holesByTee = (await dbContext.Holes
                .Where(h => ids.Contains(h.TeeboxId) && !h.IsDeleted)
                .ToListAsync())
            .GroupBy(h => h.TeeboxId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return tees
            .Select(t => (t, holesByTee.TryGetValue(t.TeeboxId, out var hs) ? hs : new List<Hole>()))
            .ToList();
    }

    /// <summary>Applies the par + handicap map to each sibling's holes in place and recomputes its par total.</summary>
    private static void ApplyCascadeInPlace(
        List<(Teebox Tee, List<Hole> Holes)> siblings,
        Dictionary<int, (int Par, int Handicap)> map, string userId, DateOnly now)
    {
        foreach (var (tee, holes) in siblings)
        {
            foreach (var h in holes)
            {
                if (map.TryGetValue(h.HoleNumber, out var v))
                {
                    h.Par = v.Par;
                    h.Handicap = v.Handicap;
                    h.UpdatedBy = userId;
                    h.UpdatedOn = now;
                }
            }
            tee.Par = holes.Sum(h => h.Par);
            tee.UpdatedBy = userId;
            tee.UpdatedOn = now;
        }
    }

    /// <summary>
    /// Creates a new version of a sibling tee: copies its own rating/slope/yardage/name/flags and
    /// lineage, applies the cascaded par + handicap (keeping each hole's own yardage), and archives
    /// the old version.
    /// </summary>
    private async Task CreateSiblingVersionAsync(
        ApplicationDbContext dbContext, Teebox tee, List<Hole> holes,
        Dictionary<int, (int Par, int Handicap)> map, string userId, DateOnly now)
    {
        int ParOf(Hole h) => map.TryGetValue(h.HoleNumber, out var v) ? v.Par : h.Par;
        int HandicapOf(Hole h) => map.TryGetValue(h.HoleNumber, out var v) ? v.Handicap : h.Handicap;

        var newTee = new Teebox
        {
            CourseId = tee.CourseId,
            TeeboxName = tee.TeeboxName,
            Par = holes.Sum(ParOf),
            Rating = tee.Rating,
            Slope = tee.Slope,
            YardageOut = tee.YardageOut,
            YardageIn = tee.YardageIn,
            YardageTotal = tee.YardageTotal,
            IsNineHole = tee.IsNineHole,
            IsWomens = tee.IsWomens,
            TeeboxGroupId = tee.TeeboxGroupId,
            ArchivedOn = null,
            ArchivedBy = null,
            CreatedBy = userId,
            CreatedOn = now,
            UpdatedBy = userId,
            UpdatedOn = now,
            IsDeleted = false
        };

        dbContext.Teeboxes.Add(newTee);
        await dbContext.SaveChangesAsync();

        dbContext.Holes.AddRange(holes.Select(h => new Hole
        {
            TeeboxId = newTee.TeeboxId,
            CourseId = tee.CourseId,
            HoleNumber = h.HoleNumber,
            Par = ParOf(h),
            Yardage = h.Yardage,
            Handicap = HandicapOf(h),
            CreatedBy = userId,
            CreatedOn = now,
            UpdatedBy = userId,
            UpdatedOn = now,
            IsDeleted = false
        }));

        tee.ArchivedOn = now;
        tee.ArchivedBy = userId;
        tee.UpdatedBy = userId;
        tee.UpdatedOn = now;
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
