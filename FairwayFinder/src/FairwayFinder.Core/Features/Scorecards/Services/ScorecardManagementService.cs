using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.ResponseModels;
using FairwayFinder.Core.Features.Scorecards.Repositories;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Scorecards.Services;

public class ScorecardManagementService
{
    private readonly ILogger<ScorecardManagementService> _logger;
    private readonly CourseLookupService _courseLookupService;
    private readonly TeeboxLookupService _teeboxLookupService;
    private readonly HoleLookupService _holeLookupService;
    private readonly IScorecardManagementRepository _repository;
    private readonly IScorecardRepository _scorecardRepository;
    private readonly IUsernameRetriever _usernameRetriever;

    public ScorecardManagementService(ILogger<ScorecardManagementService> logger, CourseLookupService courseLookupService, TeeboxLookupService teeboxLookupService, HoleLookupService holeLookupService, IUsernameRetriever usernameRetriever, IScorecardManagementRepository repository, IScorecardRepository scorecardRepository)
    {
        _logger = logger;
        _courseLookupService = courseLookupService;
        _teeboxLookupService = teeboxLookupService;
        _holeLookupService = holeLookupService;
        _usernameRetriever = usernameRetriever;
        _repository = repository;
        _scorecardRepository = scorecardRepository;
    }

    public async Task<int> CreateNewScorecardAsync(ScorecardFormModel form)
    {
        var userId = _usernameRetriever.Username;
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
            var userId = _usernameRetriever.Username;
            
            // Validate round ID from the form
            if (form.RoundFormModel.RoundId is null)
            {
                _logger.LogError("Round Id was null when trying to update round.");
                return false;
            }

            var roundId = form.RoundFormModel.RoundId.Value;

            // 4 tables that need to be updated. We will send all off at once
            var roundTask = _scorecardRepository.GetRoundByIdAsync(roundId);
            var holeScoresTask = _scorecardRepository.GetScoresForRoundByRoundIdAsync(roundId);
            var roundStatsTask = _scorecardRepository.GetRoundStatsByRoundIdAsync(roundId);
            var holeStatsTask = _scorecardRepository.GetHoleStatsByRoundAsync(roundId);

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
            
            return await _repository.UpdateScorecardAsync(updated_round, updated_scores, updated_round_stats, updated_hole_stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred trying to update round id {0}", form.RoundFormModel.RoundId);
            return false;
        }
    }


    private Round CreateRoundFromForm(ScorecardFormModel form, string userId)
    {
        // Create our round model
        var round = form.RoundFormModel.ToModel();
        
        round.score = form.HoleScore.Sum(x => x.Score);
        round.score_out = form.HoleScore.Where(x => x.HoleNumber <= 9).Sum(x => x.Score);
        round.score_in = form.HoleScore.Where(x => x.HoleNumber > 9).Sum(x => x.Score);
        round.user_id = _usernameRetriever.UserId; // TODO: Change this to be the userId passed in. Right now its just username passed in until all records are updated.
        
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
            var updatedStat = form.HoleScore.FirstOrDefault(h => h.HoleStats.HoleStatsId == hs.hole_stats_id);
            
            if (updatedStat is not null)
            {
                hs.hit_fairway = updatedStat.HoleStats.HitFairway ? true : updatedStat.HoleStats.MissedFairway ? false : null;
                hs.hit_green = updatedStat.HoleStats.HitGreen ? true : updatedStat.HoleStats.MissedGreen ? false : null;
            
                // If we hit, it should be null. If we miss it should have a value. If both are false, then it should be null.
                hs.miss_fairway_type = updatedStat.HoleStats.HitFairway || !updatedStat.HoleStats.MissedFairway ? null : updatedStat.HoleStats.MissFairwayType;
                hs.miss_green_type = updatedStat.HoleStats.HitGreen || !updatedStat.HoleStats.MissedGreen ? null : updatedStat.HoleStats.MissGreenType;

                hs.number_of_putts = updatedStat.HoleStats.NumberOfPutts;
                hs.approach_yardage = updatedStat.HoleStats.YardageOut;
                
                // Update the stat and add to the new list
                updatedHoleStats.Add(EntityMetadataHelper.UpdateRecord(hs, userId));
            }
        }

        return updatedHoleStats;
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
}