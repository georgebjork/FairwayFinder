using FairwayFinder.Core.Enums;
using FairwayFinder.Core.Features.GolfCourse.Models;
using FairwayFinder.Core.Features.GolfCourse.Models.ResponseModels;
using FairwayFinder.Core.Features.GolfCourse.Repository.Interfaces;
using FairwayFinder.Core.Features.GolfCourse.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.GolfCourse.Services;

public class CourseStatsService : ICourseStatsService
{
    private readonly ILogger<CourseStatsService> _logger;
    private readonly ICourseStatsRepository _repository;

    public CourseStatsService(ILogger<CourseStatsService> logger, ICourseStatsRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public async Task<CourseStatsResponse> GetAllCourseStatsAsync(CourseStatsRequest request)
    {
        var score_counts = await GetScoreCountsAsync(request);
        var round_scores = await GetRoundScoresAsync(request);
        var hole_stats = await GetHoleStatsAsync(request);

        return new CourseStatsResponse
        {
            ScoreCounts = score_counts,
            RoundScores = round_scores,
            HoleStats = hole_stats.OrderBy(x => x.HoleNumber).ToList()
        };
    }

    public async Task<CourseStatsRoundsResponse> GetRoundScoresAsync(CourseStatsRequest request)
    {
        var rounds = await _repository.GetRoundsListAsync(request);

        var response = new CourseStatsRoundsResponse();
        
        // Make sure we have 18 hole rounds
        if (rounds.Any(x => x.FullRound))
        {
            response.RoundsCount = rounds.Count(x => x.FullRound);
            response.AvgScore = Math.Round(rounds.Where(x => x.FullRound).Average(x => x.Score), 2);
            response.LowScore = rounds.Where(x => x.FullRound).Min(x => x.Score);
            response.HighScore = rounds.Where(x => x.FullRound).Max(x => x.Score);
        }
        
        // Make sure we have 9 hole rounds.
        if (rounds.Any(x => !x.FullRound))
        {
            response.RoundsNineHoleCount = rounds.Count(x => !x.FullRound);
            response.AvgNineHoleScore = Math.Round(rounds.Where(x => !x.FullRound).Average(x => x.Score), 2);
            response.LowNineHoleScore = rounds.Where(x => !x.FullRound).Min(x => x.Score);
            response.HighNineHoleScore = rounds.Where(x => !x.FullRound).Max(x => x.Score);
        }

        return response;
    }

    public async Task<List<CourseStatsHoleStatsResponse>> GetHoleStatsAsync(CourseStatsRequest request)
    {
        return await _repository.GetHoleStatsAsync(request);
    }

    public async Task<CourseStatsScoreCountsResponse> GetScoreCountsAsync(CourseStatsRequest request)
    {
        var scores = await _repository.GetScoresListAsync(request);

        var counts = new Dictionary<ScoreType, int>
        {
            { ScoreType.HoleInOne, 0 },
            { ScoreType.Albatross, 0 },
            { ScoreType.Eagle, 0 },
            { ScoreType.Birdie, 0 },
            { ScoreType.Par, 0 },
            { ScoreType.Bogey, 0 },
            { ScoreType.DoubleBogey, 0 },
            { ScoreType.Worse, 0 }
        };

        foreach (var score in scores)
        {
            var diff = score.HoleScore - score.Par;

            if (score.HoleScore == 1)
                counts[ScoreType.HoleInOne]++;

            if (diff == -3)
                counts[ScoreType.Albatross]++;
            else if (diff == -2)
                counts[ScoreType.Eagle]++;
            else if (diff == -1)
                counts[ScoreType.Birdie]++;
            else if (diff == 0)
                counts[ScoreType.Par]++;
            else if (diff == 1)
                counts[ScoreType.Bogey]++;
            else if (diff == 2)
                counts[ScoreType.DoubleBogey]++;
            else if (diff >= 3)
                counts[ScoreType.Worse]++;
        }

        return new CourseStatsScoreCountsResponse
        {
            HoleInOnes = counts[ScoreType.HoleInOne],
            Albatross = counts[ScoreType.Albatross],
            Eagle = counts[ScoreType.Eagle],
            Birdie = counts[ScoreType.Birdie],
            Par = counts[ScoreType.Par],
            Bogey = counts[ScoreType.Bogey],
            DoubleBogey = counts[ScoreType.DoubleBogey],
            Worse = counts[ScoreType.Worse]
        };
    }
}