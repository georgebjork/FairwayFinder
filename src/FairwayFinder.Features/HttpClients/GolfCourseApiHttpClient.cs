using System.Net.Http.Json;
using FairwayFinder.Features.Data.GolfCourseApi;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.HttpClients;

public class GolfCourseApiHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GolfCourseApiHttpClient> _logger;

    public GolfCourseApiHttpClient(HttpClient httpClient, ILogger<GolfCourseApiHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GolfCourseApiCoursesResponse?> GetCoursesAsync(int page, int pageSize = 100, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/v1/courses?page={page}&page_size={pageSize}", ct);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GolfCourseApiCoursesResponse>(cancellationToken: ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch courses from GolfCourseAPI (page {Page}, pageSize {PageSize})", page, pageSize);
            throw;
        }
    }
}
