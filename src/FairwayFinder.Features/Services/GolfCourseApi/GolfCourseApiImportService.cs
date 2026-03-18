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
                _logger.LogInformation("Import progress: {PagesProcessed}/{TotalPages} pages, {Imported} imported, {Skipped} skipped",
                    result.PagesProcessed, result.TotalPages, result.CoursesImported, result.CoursesSkipped);
            }
        }

        _logger.LogInformation("GolfCourseAPI import complete: {Imported} imported, {Skipped} skipped, {Errors} errors",
            result.CoursesImported, result.CoursesSkipped, result.Errors.Count);

        return result;
    }

    private async Task ProcessPageAsync(GolfCourseApiCoursesResponse pageResponse, GolfCourseApiImportResult result, CancellationToken ct)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);

        // Load all existing mappings for the API course IDs on this page to avoid per-course queries
        var apiCourseIds = pageResponse.Courses
            .Select(c => c.Id)
            .ToList();

        var existingMappings = await dbContext.GolfCourseApiCourseMaps
            .Where(m => apiCourseIds.Contains(m.ApiCourseId))
            .Select(m => m.ApiCourseId)
            .ToHashSetAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var apiCourse in pageResponse.Courses)
        {

            try
            {
                if (existingMappings.Contains(apiCourse.Id))
                {
                    result.CoursesSkipped++;
                    continue;
                }

                // Create the Course entity
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
                await dbContext.SaveChangesAsync(ct); // Save to get the generated CourseId

                // Create teeboxes and holes
                if (apiCourse.Tees is not null)
                {
                    await CreateTeeboxesAsync(dbContext, course.CourseId, apiCourse.Tees.Male, false, today, ct);
                    await CreateTeeboxesAsync(dbContext, course.CourseId, apiCourse.Tees.Female, true, today, ct);
                }

                // Create the mapping record
                var mapping = new GolfCourseApiCourseMap
                {
                    ApiCourseId = apiCourse.Id,
                    CourseId = course.CourseId
                };

                dbContext.GolfCourseApiCourseMaps.Add(mapping);
                await dbContext.SaveChangesAsync(ct);

                result.CoursesImported++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import course {ApiCourseId} ({CourseName})", apiCourse.Id, apiCourse.CourseName);
                result.Errors.Add(new GolfCourseApiImportError
                {
                    ApiCourseId = apiCourse.Id,
                    CourseName = apiCourse.CourseName,
                    Reason = ex.Message
                });
            }
        }
    }

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

            // Calculate yardage out/in from hole data
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
            await dbContext.SaveChangesAsync(ct); // Save to get TeeboxId

            // Create holes
            for (var i = 0; i < holes.Count; i++)
            {
                var apiHole = holes[i];
                var hole = new Hole
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
                };

                dbContext.Holes.Add(hole);
            }

            await dbContext.SaveChangesAsync(ct);
        }
    }

    private static GolfCourseApiImportResult CloneResult(GolfCourseApiImportResult source)
    {
        return new GolfCourseApiImportResult
        {
            CoursesImported = source.CoursesImported,
            CoursesSkipped = source.CoursesSkipped,
            TotalPages = source.TotalPages,
            PagesProcessed = source.PagesProcessed,
            TotalRecords = source.TotalRecords,
            Errors = [..source.Errors]
        };
    }
}
