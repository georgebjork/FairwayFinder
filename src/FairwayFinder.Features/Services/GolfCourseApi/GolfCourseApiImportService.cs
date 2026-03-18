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

    // ── Insert (new course) — 3 saves total ──

    private async Task InsertCourseAsync(ApplicationDbContext dbContext, GolfCourseApiCourse apiCourse, DateOnly today, CancellationToken ct)
    {
        // Save 1: Insert course to get CourseId
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

        // Save 2: Insert ALL teeboxes (both genders) to get TeeboxIds
        var teeboxApiMap = new List<(Teebox Teebox, List<GolfCourseApiHole> ApiHoles)>();

        if (apiCourse.Tees is not null)
        {
            AddTeeboxesToContext(dbContext, course.CourseId, apiCourse.Tees.Male, false, today, teeboxApiMap);
            AddTeeboxesToContext(dbContext, course.CourseId, apiCourse.Tees.Female, true, today, teeboxApiMap);
        }

        if (teeboxApiMap.Count > 0)
            await dbContext.SaveChangesAsync(ct); // All teeboxes now have their TeeboxIds

        // Save 3: Insert ALL holes for ALL teeboxes + the mapping record
        foreach (var (teebox, apiHoles) in teeboxApiMap)
        {
            for (var i = 0; i < apiHoles.Count; i++)
            {
                var apiHole = apiHoles[i];
                dbContext.Holes.Add(new Hole
                {
                    TeeboxId = teebox.TeeboxId,
                    CourseId = course.CourseId,
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
        }

        dbContext.GolfCourseApiCourseMaps.Add(new GolfCourseApiCourseMap
        {
            ApiCourseId = apiCourse.Id,
            CourseId = course.CourseId
        });

        await dbContext.SaveChangesAsync(ct);
    }

    private static void AddTeeboxesToContext(
        ApplicationDbContext dbContext,
        long courseId,
        List<GolfCourseApiTeeBox> apiTeeBoxes,
        bool isWomens,
        DateOnly today,
        List<(Teebox, List<GolfCourseApiHole>)> teeboxApiMap)
    {
        foreach (var apiTee in apiTeeBoxes)
        {
            var isNineHole = apiTee.NumberOfHoles == 9;
            var holes = apiTee.Holes;

            int yardageOut, yardageIn;
            if (isNineHole)
            {
                yardageOut = holes.Sum(h => h.Yardage);
                yardageIn = 0;
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
            teeboxApiMap.Add((teebox, holes));
        }
    }

    // ── Update (existing mapped course) — 3 queries + 1 save ──

    private async Task UpdateCourseAsync(ApplicationDbContext dbContext, long courseId, GolfCourseApiCourse apiCourse, DateOnly today, CancellationToken ct)
    {
        // Query 1: Load the course
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

        // Query 2: Load all non-deleted teeboxes for this course
        var existingTeeboxes = await dbContext.Teeboxes
            .Where(t => t.CourseId == courseId && !t.IsDeleted)
            .ToListAsync(ct);

        // Query 3: Load all non-deleted holes for this course in one query
        var existingHoles = await dbContext.Holes
            .Where(h => h.CourseId == courseId && !h.IsDeleted)
            .ToListAsync(ct);

        var holesByTeebox = existingHoles
            .GroupBy(h => h.TeeboxId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Collect new teeboxes that need TeeboxIds before we can add holes
        var newTeeboxHoles = new List<(Teebox Teebox, List<GolfCourseApiHole> ApiHoles)>();

        // Process both genders
        if (apiCourse.Tees is not null)
        {
            ProcessTeeboxUpdates(dbContext, courseId, apiCourse.Tees.Male, false, existingTeeboxes, holesByTeebox, today, newTeeboxHoles);
            ProcessTeeboxUpdates(dbContext, courseId, apiCourse.Tees.Female, true, existingTeeboxes, holesByTeebox, today, newTeeboxHoles);
        }

        // If there are new teeboxes, save to get their IDs, then add their holes
        if (newTeeboxHoles.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct); // Save 1: course updates + teebox updates + new teeboxes

            foreach (var (teebox, apiHoles) in newTeeboxHoles)
            {
                for (var i = 0; i < apiHoles.Count; i++)
                {
                    var apiHole = apiHoles[i];
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
            }
        }

        // Save everything (or Save 2 if we had new teeboxes)
        await dbContext.SaveChangesAsync(ct);
    }

    private static void ProcessTeeboxUpdates(
        ApplicationDbContext dbContext,
        long courseId,
        List<GolfCourseApiTeeBox> apiTeeBoxes,
        bool isWomens,
        List<Teebox> existingTeeboxes,
        Dictionary<long, List<Hole>> holesByTeebox,
        DateOnly today,
        List<(Teebox, List<GolfCourseApiHole>)> newTeeboxHoles)
    {
        var genderTeeboxes = existingTeeboxes.Where(t => t.IsWomens == isWomens).ToList();
        var existingByName = genderTeeboxes.ToDictionary(t => t.TeeboxName, t => t);
        var matchedNames = new HashSet<string>();

        foreach (var apiTee in apiTeeBoxes)
        {
            var isNineHole = apiTee.NumberOfHoles == 9;
            var holes = apiTee.Holes;

            int yardageOut, yardageIn;
            if (isNineHole)
            {
                yardageOut = holes.Sum(h => h.Yardage);
                yardageIn = 0;
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

                // Update holes in-place
                var teeboxHoles = holesByTeebox.GetValueOrDefault(existingTeebox.TeeboxId, []);
                var existingByNumber = teeboxHoles.ToDictionary(h => h.HoleNumber, h => h);
                var matchedNumbers = new HashSet<int>();

                for (var i = 0; i < holes.Count; i++)
                {
                    var apiHole = holes[i];
                    var holeNumber = i + 1;

                    if (existingByNumber.TryGetValue(holeNumber, out var existingHole))
                    {
                        matchedNumbers.Add(holeNumber);
                        existingHole.Par = apiHole.Par;
                        existingHole.Yardage = apiHole.Yardage;
                        existingHole.Handicap = apiHole.Handicap;
                        existingHole.UpdatedBy = SystemUser;
                        existingHole.UpdatedOn = today;
                    }
                    else
                    {
                        dbContext.Holes.Add(new Hole
                        {
                            TeeboxId = existingTeebox.TeeboxId,
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

                // Soft-delete removed holes
                foreach (var hole in teeboxHoles)
                {
                    if (matchedNumbers.Contains(hole.HoleNumber)) continue;
                    hole.IsDeleted = true;
                    hole.UpdatedBy = SystemUser;
                    hole.UpdatedOn = today;
                }
            }
            else
            {
                // New teebox — add to context, holes added after save (need TeeboxId)
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
                newTeeboxHoles.Add((teebox, holes));
            }
        }

        // Soft-delete teeboxes that no longer exist in the API
        foreach (var existingTeebox in genderTeeboxes)
        {
            if (matchedNames.Contains(existingTeebox.TeeboxName)) continue;

            existingTeebox.IsDeleted = true;
            existingTeebox.UpdatedBy = SystemUser;
            existingTeebox.UpdatedOn = today;

            // Soft-delete their holes too
            var orphanedHoles = holesByTeebox.GetValueOrDefault(existingTeebox.TeeboxId, []);
            foreach (var hole in orphanedHoles)
            {
                hole.IsDeleted = true;
                hole.UpdatedBy = SystemUser;
                hole.UpdatedOn = today;
            }
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
