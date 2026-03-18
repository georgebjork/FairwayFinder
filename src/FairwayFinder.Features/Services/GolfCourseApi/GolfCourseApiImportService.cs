using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data.GolfCourseApi;
using FairwayFinder.Features.HttpClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Services.GolfCourseApi;

public class GolfCourseApiImportService
{
    private readonly GolfCourseApiHttpClient _httpClient;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<GolfCourseApiImportService> _logger;

    private const int PageSize = 100;
    private const int RateLimitDelayMs = 200;
    private const string SystemUser = "system";

    public GolfCourseApiImportService(
        GolfCourseApiHttpClient httpClient,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<GolfCourseApiImportService> logger)
    {
        _httpClient = httpClient;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    // ── Mapping CRUD ──

    public async Task<List<GolfCourseApiCourseMap>> GetMappingsAsync()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.GolfCourseApiCourseMaps
            .Include(m => m.Course)
            .OrderBy(m => m.ApiCourseId)
            .ToListAsync();
    }

    public async Task AddMappingAsync(int apiCourseId, long courseId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // Verify the course exists
        var courseExists = await dbContext.Courses.AnyAsync(c => c.CourseId == courseId && !c.IsDeleted);
        if (!courseExists)
            throw new InvalidOperationException($"Course with ID {courseId} does not exist.");

        // Verify the mapping doesn't already exist
        var mappingExists = await dbContext.GolfCourseApiCourseMaps.AnyAsync(m => m.ApiCourseId == apiCourseId);
        if (mappingExists)
            throw new InvalidOperationException($"A mapping for API Course ID {apiCourseId} already exists.");

        dbContext.GolfCourseApiCourseMaps.Add(new GolfCourseApiCourseMap
        {
            ApiCourseId = apiCourseId,
            CourseId = courseId
        });

        await dbContext.SaveChangesAsync();
    }

    // ── Import ──

    public async Task<GolfCourseApiImportResult> ImportAllCoursesAsync(
        IProgress<GolfCourseApiImportResult>? progress = null,
        CancellationToken ct = default)
    {
        var result = new GolfCourseApiImportResult();

        // Fetch the first page to get metadata
        var firstPage = await _httpClient.GetCoursesAsync(1, PageSize, ct);
        if (firstPage is null)
        {
            _logger.LogError("Failed to fetch first page from GolfCourseAPI");
            result.Errors.Add(new GolfCourseApiImportError
            {
                Reason = "Failed to fetch first page from the API."
            });
            return result;
        }

        result.TotalPages = firstPage.Metadata.LastPage;
        result.TotalRecords = firstPage.Metadata.TotalRecords;

        _logger.LogInformation("Starting GolfCourseAPI import: {TotalRecords} courses across {TotalPages} pages",
            result.TotalRecords, result.TotalPages);

        // Process the first page
        await ProcessPageAsync(firstPage, result, ct);
        result.PagesProcessed = 1;
        progress?.Report(CloneResult(result));

        // Process remaining pages
        for (var page = 2; page <= result.TotalPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            // Rate limiting
            await Task.Delay(RateLimitDelayMs, ct);

            try
            {
                var pageResponse = await _httpClient.GetCoursesAsync(page, PageSize, ct);
                if (pageResponse is null)
                {
                    _logger.LogWarning("Null response for page {Page}, skipping", page);
                    result.PagesProcessed = page;
                    progress?.Report(CloneResult(result));
                    continue;
                }

                await ProcessPageAsync(pageResponse, result, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch page {Page}, skipping", page);
                result.Errors.Add(new GolfCourseApiImportError
                {
                    Reason = $"Failed to fetch page {page}: {ex.Message}"
                });
            }

            result.PagesProcessed = page;
            progress?.Report(CloneResult(result));

            if (page % 50 == 0)
            {
                _logger.LogInformation(
                    "Import progress: {PagesProcessed}/{TotalPages} pages, {Imported} imported, {Updated} updated, {Skipped} skipped",
                    result.PagesProcessed, result.TotalPages, result.CoursesImported, result.CoursesUpdated, result.CoursesSkipped);
            }
        }

        _logger.LogInformation(
            "GolfCourseAPI import complete: {Imported} imported, {Updated} updated, {Skipped} skipped, {Errors} errors",
            result.CoursesImported, result.CoursesUpdated, result.CoursesSkipped, result.Errors.Count);

        return result;
    }

    private async Task ProcessPageAsync(GolfCourseApiCoursesResponse pageResponse, GolfCourseApiImportResult result, CancellationToken ct)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Load existing mappings for API course IDs on this page (full objects, not just IDs)
        var apiCourseIds = pageResponse.Courses
            .Select(c => c.Id)
            .ToList();

        var existingMappings = await dbContext.GolfCourseApiCourseMaps
            .Where(m => apiCourseIds.Contains(m.ApiCourseId))
            .ToDictionaryAsync(m => m.ApiCourseId, m => m.CourseId, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var apiCourse in pageResponse.Courses)
        {
            try
            {
                if (existingMappings.TryGetValue(apiCourse.Id, out var localCourseId))
                {
                    // Mapping exists — update the course in-place
                    await UpdateCourseAsync(dbContext, localCourseId, apiCourse, today, ct);
                    result.CoursesUpdated++;
                }
                else
                {
                    // No mapping — insert new course
                    await InsertCourseAsync(dbContext, apiCourse, today, ct);
                    result.CoursesImported++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import/update course {ApiCourseId} ({CourseName})", apiCourse.Id, apiCourse.CourseName);
                result.Errors.Add(new GolfCourseApiImportError
                {
                    ApiCourseId = apiCourse.Id,
                    CourseName = apiCourse.CourseName,
                    Reason = ex.Message
                });
            }
        }
    }

    // ── Insert (new course) ──

    private async Task InsertCourseAsync(ApplicationDbContext dbContext, GolfCourseApiCourse apiCourse, DateOnly today, CancellationToken ct)
    {
        var course = new Course
        {
            CourseName = apiCourse.CourseName,
            ClubName = apiCourse.ClubName,
            Address = apiCourse.Location?.Address,
            City = apiCourse.Location?.City,
            State = apiCourse.Location?.State,
            Country = apiCourse.Location?.Country,
            Latitude = apiCourse.Location?.Latitude,
            Longitude = apiCourse.Location?.Longitude,
            CreatedBy = SystemUser,
            CreatedOn = today,
            UpdatedBy = SystemUser,
            UpdatedOn = today,
            IsDeleted = false
        };

        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync(ct);

        // Create teeboxes and holes
        if (apiCourse.Tees is not null)
        {
            await CreateTeeboxesAsync(dbContext, course.CourseId, apiCourse.Tees.Male, false, today, ct);
            await CreateTeeboxesAsync(dbContext, course.CourseId, apiCourse.Tees.Female, true, today, ct);
        }

        // Create the mapping record
        dbContext.GolfCourseApiCourseMaps.Add(new GolfCourseApiCourseMap
        {
            ApiCourseId = apiCourse.Id,
            CourseId = course.CourseId
        });

        await dbContext.SaveChangesAsync(ct);
    }

    // ── Update (existing mapped course) ──

    private async Task UpdateCourseAsync(ApplicationDbContext dbContext, long courseId, GolfCourseApiCourse apiCourse, DateOnly today, CancellationToken ct)
    {
        // Update the course record
        var course = await dbContext.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId, ct);
        if (course is null) return;

        course.CourseName = apiCourse.CourseName;
        course.ClubName = apiCourse.ClubName;
        course.Address = apiCourse.Location?.Address;
        course.City = apiCourse.Location?.City;
        course.State = apiCourse.Location?.State;
        course.Country = apiCourse.Location?.Country;
        course.Latitude = apiCourse.Location?.Latitude;
        course.Longitude = apiCourse.Location?.Longitude;
        course.UpdatedBy = SystemUser;
        course.UpdatedOn = today;

        await dbContext.SaveChangesAsync(ct);

        // Update teeboxes and holes in-place
        if (apiCourse.Tees is not null)
        {
            await UpdateTeeboxesAsync(dbContext, courseId, apiCourse.Tees.Male, false, today, ct);
            await UpdateTeeboxesAsync(dbContext, courseId, apiCourse.Tees.Female, true, today, ct);
        }
    }

    private async Task UpdateTeeboxesAsync(
        ApplicationDbContext dbContext,
        long courseId,
        List<GolfCourseApiTeeBox> apiTeeBoxes,
        bool isWomens,
        DateOnly today,
        CancellationToken ct)
    {
        // Load existing non-deleted teeboxes for this course + gender
        var existingTeeboxes = await dbContext.Teeboxes
            .Where(t => t.CourseId == courseId && t.IsWomens == isWomens && !t.IsDeleted)
            .ToListAsync(ct);

        // Build a lookup by name for matching
        var existingByName = existingTeeboxes.ToDictionary(t => t.TeeboxName, t => t);
        var matchedNames = new HashSet<string>();

        foreach (var apiTee in apiTeeBoxes)
        {
            var isNineHole = apiTee.NumberOfHoles == 9;
            var holes = apiTee.Holes;

            var yardageOut = 0;
            var yardageIn = 0;
            if (isNineHole)
            {
                yardageOut = holes.Sum(h => h.Yardage);
            }
            else
            {
                yardageOut = holes.Take(9).Sum(h => h.Yardage);
                yardageIn = holes.Skip(9).Sum(h => h.Yardage);
            }

            if (existingByName.TryGetValue(apiTee.TeeName, out var existingTeebox))
            {
                // Update existing teebox in-place
                matchedNames.Add(apiTee.TeeName);

                existingTeebox.Par = apiTee.ParTotal;
                existingTeebox.Rating = apiTee.CourseRating;
                existingTeebox.Slope = apiTee.SlopeRating;
                existingTeebox.YardageOut = yardageOut;
                existingTeebox.YardageIn = yardageIn;
                existingTeebox.YardageTotal = apiTee.TotalYards;
                existingTeebox.IsNineHole = isNineHole;
                existingTeebox.UpdatedBy = SystemUser;
                existingTeebox.UpdatedOn = today;

                await dbContext.SaveChangesAsync(ct);

                // Update holes in-place by HoleNumber
                await UpdateHolesAsync(dbContext, existingTeebox.TeeboxId, courseId, holes, today, ct);
            }
            else
            {
                // No matching teebox — create new one
                var teebox = new Teebox
                {
                    CourseId = courseId,
                    TeeboxName = apiTee.TeeName,
                    Par = apiTee.ParTotal,
                    Rating = apiTee.CourseRating,
                    Slope = apiTee.SlopeRating,
                    YardageOut = yardageOut,
                    YardageIn = yardageIn,
                    YardageTotal = apiTee.TotalYards,
                    IsNineHole = isNineHole,
                    IsWomens = isWomens,
                    CreatedBy = SystemUser,
                    CreatedOn = today,
                    UpdatedBy = SystemUser,
                    UpdatedOn = today,
                    IsDeleted = false
                };

                dbContext.Teeboxes.Add(teebox);
                await dbContext.SaveChangesAsync(ct);

                // Create holes for the new teebox
                for (var i = 0; i < holes.Count; i++)
                {
                    var apiHole = holes[i];
                    dbContext.Holes.Add(new Hole
                    {
                        TeeboxId = teebox.TeeboxId,
                        CourseId = courseId,
                        HoleNumber = i + 1,
                        Par = apiHole.Par,
                        Yardage = apiHole.Yardage,
                        Handicap = apiHole.Handicap,
                        CreatedBy = SystemUser,
                        CreatedOn = today,
                        UpdatedBy = SystemUser,
                        UpdatedOn = today,
                        IsDeleted = false
                    });
                }

                await dbContext.SaveChangesAsync(ct);
            }
        }

        // Soft-delete teeboxes that no longer exist in the API
        foreach (var existingTeebox in existingTeeboxes)
        {
            if (matchedNames.Contains(existingTeebox.TeeboxName)) continue;

            existingTeebox.IsDeleted = true;
            existingTeebox.UpdatedBy = SystemUser;
            existingTeebox.UpdatedOn = today;

            // Soft-delete their holes too
            var orphanedHoles = await dbContext.Holes
                .Where(h => h.TeeboxId == existingTeebox.TeeboxId && !h.IsDeleted)
                .ToListAsync(ct);

            foreach (var hole in orphanedHoles)
            {
                hole.IsDeleted = true;
                hole.UpdatedBy = SystemUser;
                hole.UpdatedOn = today;
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private async Task UpdateHolesAsync(
        ApplicationDbContext dbContext,
        long teeboxId,
        long courseId,
        List<GolfCourseApiHole> apiHoles,
        DateOnly today,
        CancellationToken ct)
    {
        var existingHoles = await dbContext.Holes
            .Where(h => h.TeeboxId == teeboxId && !h.IsDeleted)
            .ToListAsync(ct);

        var existingByNumber = existingHoles.ToDictionary(h => h.HoleNumber, h => h);
        var matchedNumbers = new HashSet<int>();

        for (var i = 0; i < apiHoles.Count; i++)
        {
            var apiHole = apiHoles[i];
            var holeNumber = i + 1;

            if (existingByNumber.TryGetValue(holeNumber, out var existingHole))
            {
                // Update in-place
                matchedNumbers.Add(holeNumber);

                existingHole.Par = apiHole.Par;
                existingHole.Yardage = apiHole.Yardage;
                existingHole.Handicap = apiHole.Handicap;
                existingHole.UpdatedBy = SystemUser;
                existingHole.UpdatedOn = today;
            }
            else
            {
                // Insert new hole
                dbContext.Holes.Add(new Hole
                {
                    TeeboxId = teeboxId,
                    CourseId = courseId,
                    HoleNumber = holeNumber,
                    Par = apiHole.Par,
                    Yardage = apiHole.Yardage,
                    Handicap = apiHole.Handicap,
                    CreatedBy = SystemUser,
                    CreatedOn = today,
                    UpdatedBy = SystemUser,
                    UpdatedOn = today,
                    IsDeleted = false
                });
            }
        }

        // Soft-delete holes that no longer exist in the API
        foreach (var existingHole in existingHoles)
        {
            if (matchedNumbers.Contains(existingHole.HoleNumber)) continue;

            existingHole.IsDeleted = true;
            existingHole.UpdatedBy = SystemUser;
            existingHole.UpdatedOn = today;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    // ── Create (for new inserts only) ──

    private async Task CreateTeeboxesAsync(
        ApplicationDbContext dbContext,
        long courseId,
        List<GolfCourseApiTeeBox> teeBoxes,
        bool isWomens,
        DateOnly today,
        CancellationToken ct)
    {
        foreach (var apiTee in teeBoxes)
        {
            var isNineHole = apiTee.NumberOfHoles == 9;
            var holes = apiTee.Holes;

            var yardageOut = 0;
            var yardageIn = 0;

            if (isNineHole)
            {
                yardageOut = holes.Sum(h => h.Yardage);
            }
            else
            {
                yardageOut = holes.Take(9).Sum(h => h.Yardage);
                yardageIn = holes.Skip(9).Sum(h => h.Yardage);
            }

            var teebox = new Teebox
            {
                CourseId = courseId,
                TeeboxName = apiTee.TeeName,
                Par = apiTee.ParTotal,
                Rating = apiTee.CourseRating,
                Slope = apiTee.SlopeRating,
                YardageOut = yardageOut,
                YardageIn = yardageIn,
                YardageTotal = apiTee.TotalYards,
                IsNineHole = isNineHole,
                IsWomens = isWomens,
                CreatedBy = SystemUser,
                CreatedOn = today,
                UpdatedBy = SystemUser,
                UpdatedOn = today,
                IsDeleted = false
            };

            dbContext.Teeboxes.Add(teebox);
            await dbContext.SaveChangesAsync(ct);

            for (var i = 0; i < holes.Count; i++)
            {
                var apiHole = holes[i];
                dbContext.Holes.Add(new Hole
                {
                    TeeboxId = teebox.TeeboxId,
                    CourseId = courseId,
                    HoleNumber = i + 1,
                    Par = apiHole.Par,
                    Yardage = apiHole.Yardage,
                    Handicap = apiHole.Handicap,
                    CreatedBy = SystemUser,
                    CreatedOn = today,
                    UpdatedBy = SystemUser,
                    UpdatedOn = today,
                    IsDeleted = false
                });
            }

            await dbContext.SaveChangesAsync(ct);
        }
    }

    // ── Helpers ──

    private static GolfCourseApiImportResult CloneResult(GolfCourseApiImportResult source)
    {
        return new GolfCourseApiImportResult
        {
            CoursesImported = source.CoursesImported,
            CoursesUpdated = source.CoursesUpdated,
            CoursesSkipped = source.CoursesSkipped,
            TotalPages = source.TotalPages,
            PagesProcessed = source.PagesProcessed,
            TotalRecords = source.TotalRecords,
            Errors = [..source.Errors]
        };
    }
}
