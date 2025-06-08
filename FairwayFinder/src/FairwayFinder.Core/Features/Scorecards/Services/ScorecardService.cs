using System.Text.Json;
using FairwayFinder.Core.Features.Scorecards.Cache;
using FairwayFinder.Core.Features.Scorecards.Models;
using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Features.Scorecards.Models.ResponseModels;
using FairwayFinder.Core.Features.Scorecards.Repositories;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Services.Interfaces;
using FairwayFinder.Core.Stats;
using FairwayFinder.Core.Stats.Models.QueryModels;
using FairwayFinder.Core.Stats.Repositories;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Scorecards.Services;


public interface IScorecardService
{
    Task<List<ScorecardSummaryQueryModel>> GetScorecardListByUserIdAsync(string userId, int? limit = null);
    Task<ScorecardResponseModel> GetScorecardByRoundIdAsync(long roundId);
    Task<ScorecardRoundStats> GetScorecardRoundStatsByRoundIdAsync(long roundId);
    Task<int> CreateNewScorecardAsync(ScorecardFormModel form);
    Task<bool> UpdateScorecardAsync(ScorecardFormModel form);
    Task<bool> UpdateRoundExclusion(long roundId, bool exclude);
}


public class ScorecardService : IScorecardService
{
    private readonly ILogger<ScorecardService> _logger;
    private readonly IScorecardRepository _repository;
    private readonly IScorecardStatRepository _scorecardStatRepository;
    private readonly ICourseService _courseService;
    private readonly ITeeboxService _teeboxService;
    private readonly IHoleService _holeService;
    private readonly IUsernameRetriever _usernameRetriever;

    private readonly IDistributedCache _cache;

    public ScorecardService(ILogger<ScorecardService> logger, IScorecardRepository repository, ITeeboxService teeboxService, ICourseService courseService, IUsernameRetriever usernameRetriever, IHoleService holeService, IScorecardStatRepository scorecardStatRepository, IDistributedCache cache)
    {
        _logger = logger;
        _repository = repository;
        _teeboxService = teeboxService;
        _courseService = courseService;
        _usernameRetriever = usernameRetriever;
        _holeService = holeService;
        _scorecardStatRepository = scorecardStatRepository;
        _cache = cache;
    }
    
    public async Task<List<ScorecardSummaryQueryModel>> GetScorecardListByUserIdAsync(string userId, int? limit = null)
    {
        return await _repository.GetScorecardListByUserIdAsync(userId, limit);
    }
    
    public async Task<ScorecardResponseModel> GetScorecardByRoundIdAsync(long roundId)
    {
        // Get the cached response if possible
        var cache_response = await _cache.GetStringAsync(ScorecardCacheKeys.GetScorecardCacheKey(roundId));
        if (cache_response is not null)
        {
            var rv = JsonSerializer.Deserialize<ScorecardResponseModel>(cache_response);
            return rv ?? new ScorecardResponseModel();
        }
        
        var response = new ScorecardResponseModel();
        
        // First check if the round exists
        var round = await _repository.GetRoundByIdAsync(roundId);
        if (round is null)
        {
            response.Success = false;
            response.ErrorMessage = "Round does not exist";
            
            _logger.LogError("User {0} tried to edit round id {1} that does not exist", _usernameRetriever.Email, roundId);
            return response;
        }
        response.Round = round;
        
        // Now go get the course, teebox and holes
        var course_task = _courseService.GetCourseByIdAsync(round.course_id);
        var teebox_task = _teeboxService.GetTeeByIdAsync(round.teebox_id);
        var holes_task = _holeService.GetHolesForTeeAsync(round.teebox_id);
        
        await Task.WhenAll(course_task, teebox_task, holes_task);

        response.Course = course_task.Result ?? new Course();
        response.Teebox = teebox_task.Result ?? new Teebox();
        response.HolesList = holes_task.Result;
        
        // Now get scores and stats
        var hole_scores_task = _repository.GetHoleScoresByRoundIdAsync(roundId);
        var hole_stats_task = _repository.GetHoleStatsByRoundIdAsync(roundId);
        var round_stats_task = _repository.GetRoundStatsByIdAsync(roundId);
        
        await Task.WhenAll(hole_scores_task, hole_stats_task, round_stats_task);

        response.HoleScoresList = hole_scores_task.Result;
        response.HoleStatsList = hole_stats_task.Result;
        response.RoundStats = round_stats_task.Result ?? new RoundStats();
        
        response.Success = true;

        // Cache this data
        await _cache.SetStringAsync(ScorecardCacheKeys.GetScorecardCacheKey(roundId), JsonSerializer.Serialize(response), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        });
        
        return response;
    }
    
    public async Task<ScorecardRoundStats> GetScorecardRoundStatsByRoundIdAsync(long roundId)
    {
        // Get the cached response if possible
        var cache_response = await _cache.GetStringAsync(ScorecardCacheKeys.GetScorecardStatsCacheKey(roundId));
        if (cache_response is not null)
        {
            var rv = JsonSerializer.Deserialize<ScorecardRoundStats>(cache_response);
            return rv ?? new ScorecardRoundStats();
        }
        
        
        var userId = _usernameRetriever.UserId;
        
        var stats = new ScorecardRoundStats();
        var scorecard_round_stats = await _scorecardStatRepository.GetScoreStatsByRoundIdAsync(roundId);
        var average_score_by_par = await _scorecardStatRepository.GetAverageScoresByParAsync(userId, new StatsRequest { RoundId = roundId });
        var hole_scores = await _repository.GetHoleScoresByRoundIdAsync(roundId);

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
        
        // Cache this data
        await _cache.SetStringAsync(ScorecardCacheKeys.GetScorecardStatsCacheKey(roundId), JsonSerializer.Serialize(stats), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        });
        
        return stats;
    }
    
    public async Task<int> CreateNewScorecardAsync(ScorecardFormModel form)
    {
        var userId = _usernameRetriever.UserId;
        try
        {
            // Create models for round and round stats
            var round = CreateRoundFromForm(form, userId);
            var round_stats = GenerateRoundStats(form.HoleScore, userId);
            round_stats = EntityMetadataHelper.NewRecord(round_stats, userId);
        
            // Create models for holes and hole scores
            var hole_scores = CreateHoleScores(form, userId);
            var hole_stats = CreateHoleStats(form, userId);

            return await _repository.CreateNewScorecardAsync(round, hole_scores, round_stats, hole_stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occured creating scorecard");
            return -1;
        }
    }

    public async Task<bool> UpdateScorecardAsync(ScorecardFormModel form)
    {
        try
        {
            var userId = _usernameRetriever.UserId;
            
            // Validate round ID from the form
            if (form.RoundFormModel.RoundId is null)
            {
                _logger.LogError("Round Id was null when trying to update round.");
                return false;
            }

            var roundId = form.RoundFormModel.RoundId.Value;

            // 4 tables that need to be updated. We will send all off at once
            var roundTask = _repository.GetRoundByIdAsync(roundId);
            var holeScoresTask = _repository.GetScoresForRoundByIdAsync(roundId);
            var roundStatsTask = _repository.GetRoundStatsByIdAsync(roundId);
            var holeStatsTask = _repository.GetHoleStatsByRoundAsync(roundId);

            await Task.WhenAll(roundTask, roundStatsTask, holeScoresTask, holeScoresTask);

            var round = await roundTask;
            var roundStats = await roundStatsTask;
            var scores = await holeScoresTask;
            var holeStats = await holeStatsTask;

            if (round is null || roundStats is null)
            {
                throw new Exception($"Round with id {roundId} came back null");
            }

            // Update round and round stats
            var updated_round = UpdateRound(round, form, userId);
            var updated_round_stats = UpdateRoundStats(roundStats, form, userId);
            
            // Update hole scores and stats
            var updated_scores = UpdateHoleScore(scores, form, userId);
            var updated_hole_stats = UpdateHoleStats(holeStats, form, userId);

            await ClearScorecardCache(roundId);
            
            return await _repository.UpdateScorecardAsync(updated_round, updated_scores, updated_round_stats, updated_hole_stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred trying to update round id {0}", form.RoundFormModel.RoundId);
            return false;
        }
    }

    public async Task<bool> UpdateRoundExclusion(long roundId, bool exclude)
    {
        var round = await _repository.GetRoundByIdAsync(roundId);

        if (round is null) return false; // Yikes round does not exist

        round.exclude_from_stats = exclude;
        round = EntityMetadataHelper.UpdateRecord(round, _usernameRetriever.UserId);

        await ClearScorecardCache(roundId);
        return await _repository.Update(round);
    }

    private Round CreateRoundFromForm(ScorecardFormModel form, string userId)
    {
        // Create our round model
        var round = form.RoundFormModel.ToModel();
        
        round.full_round = form.FullRound;
        round.front_nine = form.FrontNine;
        round.back_nine = form.BackNine;
        
        round.score = form.HoleScore.Sum(x => x.Score);
        round.score_out = form.HoleScore.Where(x => x.HoleNumber <= 9).Sum(x => x.Score);
        round.score_in = form.HoleScore.Where(x => x.HoleNumber > 9).Sum(x => x.Score);
        
        round.user_id = _usernameRetriever.UserId;
        
        round = EntityMetadataHelper.NewRecord(round, userId);
        
        return round;
    }

    private List<Score> CreateHoleScores(ScorecardFormModel form, string userId)
    {
        var holes = new List<Score>();
        foreach (var h in form.HoleScore)
        {
            var hole = new Score
            {
                hole_id = h.HoleId,
                hole_score = h.Score,
                user_id = _usernameRetriever.UserId // TODO: Change this to be the userId passed in. Right now its just username passed in until all records are updated.
            };
            holes.Add(EntityMetadataHelper.NewRecord(hole, userId));
        }

        return holes;
    }
    
    private List<HoleStats> CreateHoleStats(ScorecardFormModel form, string userId)
    {
        var hole_stats = new List<HoleStats>();
        foreach (var h in form.HoleScore)
        {
            var hole = h.HoleStats.ToModel();
            hole = SetHoleStatsFromForm(h.HoleStats, hole);

            if (!form.RoundFormModel.UsingHoleStats)
            {
                hole.hit_fairway = null;
                hole.hit_green = null;
            }
            hole_stats.Add(EntityMetadataHelper.NewRecord(hole, userId));
        }

        return hole_stats;
    }

    private RoundStats GenerateRoundStats(List<HoleScoreFormModel> holes, string userId)
    {
        var round_stats = new RoundStats
        {
            hole_in_one = holes.Count(x => x.Score == 1),
            double_eagles = holes.Count(x => x.Score == x.Par - 3),
            eagles = holes.Count(x => x.Score == x.Par - 2),
            birdies = holes.Count(x => x.Score == x.Par - 1),
            pars = holes.Count(x => x.Score == x.Par),
            bogies = holes.Count(x => x.Score == x.Par + 1),
            double_bogies = holes.Count(x => x.Score == x.Par + 2),
            triple_or_worse = holes.Count(x => x.Score >= x.Par + 3),
        };
        return round_stats;
    }

    private Round UpdateRound(Round round, ScorecardFormModel form, string userId)
    {
        // Update round
        round.score_out = form.HoleScore.Where(x => x.HoleNumber <= 9).Sum(y => y.Score);
        round.score_in = form.HoleScore.Where(x => x.HoleNumber > 9).Sum(y => y.Score);
        round.score = round.score_out + round.score_in;
        round.date_played = form.RoundFormModel.DatePlayed;
        round.using_hole_stats = form.RoundFormModel.UsingHoleStats;

        return EntityMetadataHelper.UpdateRecord(round, userId);
    }

    private List<Score> UpdateHoleScore(List<Score> holeScores, ScorecardFormModel form, string userId)
    {
        var updatedHoleScores = new List<Score>();
        foreach (var hs in holeScores)
        {
            var updatedScore = form.HoleScore.FirstOrDefault(h => h.HoleId == hs.hole_id);
            
            if (updatedScore is not null)
            {
                // Update the score and add to the new list
                hs.hole_score = updatedScore.Score;
                updatedHoleScores.Add(EntityMetadataHelper.UpdateRecord(hs, userId));
            }
        }

        return updatedHoleScores;
    }
    
    private List<HoleStats> UpdateHoleStats(List<HoleStats> holeStats, ScorecardFormModel form, string userId)
    {
        var updatedHoleStats = new List<HoleStats>();
        foreach (var hs in holeStats)
        {
            var updatedStatForm = form.HoleScore.FirstOrDefault(h => h.HoleStats.HoleStatsId == hs.hole_stats_id);
            
            if (updatedStatForm is not null)
            {
                // Bit uninutive but we are leveraging pass by reference here.
                SetHoleStatsFromForm(updatedStatForm.HoleStats, hs);
                
                // Update the stat and add to the new list
                updatedHoleStats.Add(EntityMetadataHelper.UpdateRecord(hs, userId));
            }
        }

        return updatedHoleStats;
    }

    private HoleStats SetHoleStatsFromForm(HoleStatsFormModel form, HoleStats stats)
    {
        stats.hit_fairway = form.HitFairway ? true : form.MissedFairway ? false : null;
        stats.hit_green = form.HitGreen ? true : form.MissedGreen ? false : null;
                
        // If we hit, it should be null. If we miss it should have a value. If both are false, then it should be null.
        stats.miss_fairway_type = form.HitFairway || !form.MissedFairway ? null : form.MissFairwayType;
        stats.miss_green_type = form.HitGreen || !form.MissedGreen ? null : form.MissGreenType;
                
        stats.number_of_putts = form.NumberOfPutts;
        stats.approach_yardage = form.YardageOut;

        stats.tee_shot_ob = form.HitFairway ? null : form.TeeShotOb;
        stats.approach_shot_ob = form.HitGreen ? null : form.ApproachShotOb;

        return stats;
    }

    private RoundStats UpdateRoundStats(RoundStats roundStats, ScorecardFormModel form, string userId)
    {
        var updated_round_stats = GenerateRoundStats(form.HoleScore, userId);
        updated_round_stats.round_id = form.RoundFormModel.RoundId ?? 0;
        updated_round_stats.round_stats_id = roundStats.round_id;
        updated_round_stats.created_on = roundStats.created_on;
        updated_round_stats.created_by = roundStats.created_by;
        updated_round_stats = EntityMetadataHelper.UpdateRecord(updated_round_stats, userId);
        
        return updated_round_stats;
    }

    private async Task ClearScorecardCache(long roundId)
    {
        // Remove the cached scorecard since updated data will need to be retrived
        await _cache.RemoveAsync(ScorecardCacheKeys.GetScorecardCacheKey(roundId));
        await _cache.RemoveAsync(ScorecardCacheKeys.GetScorecardStatsCacheKey(roundId));
    }
}