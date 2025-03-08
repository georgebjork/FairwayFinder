using FairwayFinder.Core.Features.Scorecards.Models;
using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Features.Scorecards.Models.ResponseModels;
using FairwayFinder.Core.Features.Scorecards.Repositories;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Stats;
using FairwayFinder.Core.Stats.Models.QueryModels;
using FairwayFinder.Core.Stats.Repositories;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Scorecards.Services;


public interface IScorecardService
{
    public Task<List<ScorecardSummaryQueryModel>> GetScorecardListByUserIdAsync(string userId, int? limit = null);
    public Task<ScorecardResponseModel> GetScorecardByRoundIdAsync(long roundId);
    public Task<ScorecardRoundStats> GetScorecardRoundStatsByRoundIdAsync(long roundId);
}


public class ScorecardService : IScorecardService
{
    private readonly ILogger<ScorecardService> _logger;
    private readonly IScorecardRepository _scorecardRepository;
    private readonly IStatRepository _statRepository;
    private readonly CourseLookupService _courseLookupService;
    private readonly TeeboxLookupService _teeboxLookupService;
    private readonly HoleLookupService _holeLookupService;
    private readonly IUsernameRetriever _usernameRetriever;

    public ScorecardService(ILogger<ScorecardService> logger, IScorecardRepository scorecardRepository, TeeboxLookupService teeboxLookupService, CourseLookupService courseLookupService, IUsernameRetriever usernameRetriever, HoleLookupService holeLookupService, IStatRepository statRepository)
    {
        _logger = logger;
        _scorecardRepository = scorecardRepository;
        _teeboxLookupService = teeboxLookupService;
        _courseLookupService = courseLookupService;
        _usernameRetriever = usernameRetriever;
        _holeLookupService = holeLookupService;
        _statRepository = statRepository;
    }
    
    public async Task<List<ScorecardSummaryQueryModel>> GetScorecardListByUserIdAsync(string userId, int? limit = null)
    {
        return await _scorecardRepository.GetScorecardListByUserIdAsync(userId, limit);
    }
    
    public async Task<ScorecardResponseModel> GetScorecardByRoundIdAsync(long roundId)
    {
        var response = new ScorecardResponseModel();
        
        // First check if the round exists
        var round = await _scorecardRepository.GetRoundByIdAsync(roundId);
        if (round is null)
        {
            response.Success = false;
            response.ErrorMessage = "Round does not exist";
            
            _logger.LogError("User {0} tried to edit round id {1} that does not exist", _usernameRetriever.Email, roundId);
            return response;
        }
        response.Round = round;
        
        // Now go get the course, teebox and holes
        var course_task = _courseLookupService.GetCourseByIdAsync(round.course_id);
        var teebox_task = _teeboxLookupService.GetTeeByIdAsync(round.teebox_id);
        var holes_task = _holeLookupService.GetHolesForTeeAsync(round.teebox_id);
        
        await Task.WhenAll(course_task, teebox_task, holes_task);

        response.Course = course_task.Result ?? new Course();
        response.Teebox = teebox_task.Result ?? new Teebox();
        response.HolesList = holes_task.Result;
        
        // Now get scores and stats
        var hole_scores_task = _scorecardRepository.GetHoleScoresByRoundIdAsync(roundId);
        var hole_stats_task = _scorecardRepository.GetHoleStatsByRoundIdAsync(roundId);
        var round_stats_task = _scorecardRepository.GetRoundStatsByIdAsync(roundId);
        
        await Task.WhenAll(hole_scores_task, hole_stats_task, round_stats_task);

        response.HoleScoresList = hole_scores_task.Result;
        response.HoleStatsList = hole_stats_task.Result;
        response.RoundStats = round_stats_task.Result ?? new RoundStats();
        
        response.Success = true;
        return response;
    }
    
    public async Task<ScorecardRoundStats> GetScorecardRoundStatsByRoundIdAsync(long roundId)
    {
        var userId = _usernameRetriever.UserId;
        
        var stats = new ScorecardRoundStats();
        var scorecard_round_stats = await _statRepository.GetScoreStatsByRoundIdAsync(roundId);
        var average_score_by_par = await _statRepository.GetAverageScoresByParAsync(userId, new StatsRequest { RoundId = roundId });
        var hole_scores = await _scorecardRepository.GetHoleScoresByRoundIdAsync(roundId);

        stats.Par3ScoreToPar = GolfStatHelpers.ScoreToParStats(hole_scores, par: 3);
        stats.Par4ScoreToPar = GolfStatHelpers.ScoreToParStats(hole_scores, par: 4);
        stats.Par5ScoreToPar = GolfStatHelpers.ScoreToParStats(hole_scores, par: 5);

        stats.Par3AverageScoreToPar =
            Math.Round(average_score_by_par.FirstOrDefault(x => x.par == 3)?.average_score ?? 0, 2,
                MidpointRounding.AwayFromZero);
        
        stats.Par4AverageScoreToPar =
            Math.Round(average_score_by_par.FirstOrDefault(x => x.par == 4)?.average_score ?? 0, 2,
                MidpointRounding.AwayFromZero);
        
        stats.Par5AverageScoreToPar =
            Math.Round(average_score_by_par.FirstOrDefault(x => x.par == 4)?.average_score ?? 0, 2,
                MidpointRounding.AwayFromZero);
       
        stats.ScoreCountStatsQueryModel = scorecard_round_stats;
        stats.AverageScoreByParQueryModel = average_score_by_par.OrderBy(x => x.par).ToList();
        return stats;
    }
}