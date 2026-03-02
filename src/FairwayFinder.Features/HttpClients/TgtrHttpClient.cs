using System.Net.Http.Json;
using FairwayFinder.Features.Data.TGTR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.HttpClients;

public class TgtrHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TgtrHttpClient> _logger;

    public TgtrHttpClient(HttpClient httpClient, ILogger<TgtrHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<TgtrRoundResponse>> GetPlayerRoundsAsync(int tgtrPlayerId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/players/{tgtrPlayerId}/rounds");
            response.EnsureSuccessStatusCode();

            var rounds = await response.Content.ReadFromJsonAsync<List<TgtrRoundResponse>>();
            return rounds ?? new List<TgtrRoundResponse>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch rounds from TGTR API for player {TgtrPlayerId}", tgtrPlayerId);
            throw;
        }
    }
}
