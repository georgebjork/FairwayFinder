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

public class ScorecardService 
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
    
    
    public async Task<ScorecardResponseModel> GetScorecardAsync(long roundId)
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
        var hole_scores_task = _scorecardRepository.GetHoleScoresByRoundIdAsync(round.round_id);
        var hole_stats_task = _scorecardRepository.GetHoleStatsByRoundAsync(round.round_id);
        
        await Task.WhenAll(hole_scores_task, hole_stats_task);

        response.ScoresList = hole_scores_task.Result;
        response.HoleStatsList = hole_stats_task.Result;
        
        response.Success = true;
        return response;
    }
    

    public async Task<List<RoundSummaryQueryModel>> GetRoundSummaryByUserId(string userId, int? limit = null)
    {
        return await _scorecardRepository.GetRoundsSummaryByUserIdAsync(userId, limit);
    }
    
    public async Task<RoundSummaryQueryModel?> GetScorecardSummaryByRoundIdAsync(long roundId)
    {
        return await _scorecardRepository.GetScorecardSummaryByRoundIdAsync(roundId);
    }

    public async Task<List<HoleScoreQueryModel>> GetScorecardHoleScoresByRoundIdAsync(long roundId)
    {
        return await _scorecardRepository.GetHoleScoresByRoundIdAsync(roundId);
    }
    
    public async Task<ScorecardRoundStats> GetScorecardRoundStatsAsync(long roundId)
    {
        var userId = _usernameRetriever.UserId;
        
        var stats = new ScorecardRoundStats();
        var scorecard_round_stats = await _statRepository.GetScoreStatsByRoundIdAsync(roundId);
        var average_score_by_par = await _statRepository.GetAverageScoresByParAsync(userId, new StatsRequest { RoundId = roundId });
        var hole_scores = await GetScorecardHoleScoresByRoundIdAsync(roundId);

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
    
    public async Task<List<HoleStatsQueryModel>> GetHoleStatsByRoundIdAsync(long roundId)
    {
        return await _scorecardRepository.GetHoleStatsByRoundIdAsync(roundId);
    }
    
    public async Task<Round?> GetScorecardByIdAsync(long roundId)
    {
        return await _scorecardRepository.GetScorecardByIdAsync(roundId);
    }
    
    public async Task<List<HoleScoreFormModel>> GetHoleScoreFormsByRoundIdAsync(long roundId)
    {
        var holes = await _holeLookupService.GetHolesForRoundByRoundIdAsync(roundId);
        var scores = await _scorecardRepository.GetHoleScoresByRoundIdAsync(roundId);
        var score_forms = new List<HoleScoreFormModel>();

        foreach (var score in scores)
        {
            var h = holes.FirstOrDefault(x => x.hole_id == score.hole_id);
            score_forms.Add(new HoleScoreFormModel
            {
                HoleId = score.hole_id,
                ScoreId = score.score_id,
                HoleNumber = score.hole_number,
                Score = score.hole_score,
                Par = score.par,
                Yardage = h.yardage
            });
        }

        return score_forms;
    }
    
    public async Task<List<HoleStatsFormModel>> GetHoleScoreStatsFormsByRoundIdAsync(long roundId)
    {
        var hole_stats = await _scorecardRepository.GetHoleStatsByRoundAsync(roundId);
        var hole_stats_form = hole_stats.Select(stats => stats.ToForm()).ToList();
        
        return hole_stats_form;
    }

    public async Task<int> CreateNewScorecardAsync(ScorecardFormModel form)
    {
        // Validate that the course exists
        var course = await _courseLookupService.GetCourseByIdAsync(form.RoundFormModel.CourseId);
        if (course == null)
        {
            _logger.LogError("Course not found for CourseId: {CourseId}", form.RoundFormModel.CourseId);
            return -1;
        }

        // Validate that the teebox exists
        var teebox = await _teeboxLookupService.GetTeeByIdAsync(form.RoundFormModel.TeeboxId);
        if (teebox == null)
        {
            _logger.LogError("Teebox not found for TeeboxId: {TeeboxId}", form.RoundFormModel.TeeboxId);
            return -1;
        }

        if (form.HoleScore.Count <= 0)
        {
            _logger.LogError("No scores were included in form");
            return -1;
        }

        try
        {
            var username = _usernameRetriever.Username;
            var user_id = _usernameRetriever.UserId;
            
            // Round
            var round = new Round()
            {
                course_id = form.RoundFormModel.CourseId,
                teebox_id = form.RoundFormModel.TeeboxId,
                date_played = form.RoundFormModel.DatePlayed,
                user_id = user_id,
                score = form.HoleScore.Sum(x => x.Score),
                score_out = form.HoleScore.Where(x => x.HoleNumber <= 9).Sum(x => x.Score),
                score_in = form.HoleScore.Where(x => x.HoleNumber > 9).Sum(x => x.Score),
                using_hole_stats = form.RoundFormModel.UsingHoleStats
            };
            round = EntityMetadataHelper.NewRecord(round, username);
            
            // Round stats
            var round_stats = GolfStatHelpers.GenerateRoundStats(form.HoleScore);
            round_stats = EntityMetadataHelper.NewRecord(round_stats, username);
            
            // Hole Scores
            var holes = new List<Score>();
            foreach (var h in form.HoleScore)
            {
                var hole = new Score
                {
                    hole_id = h.HoleId,
                    hole_score = h.Score,
                    user_id = user_id
                };
                holes.Add(EntityMetadataHelper.NewRecord(hole, username));
            }
            
            var hole_stats = new List<HoleStats>();
            foreach (var h in form.HoleScore)
            {
                var hole = h.HoleStats.ToModel();

                if (!form.RoundFormModel.UsingHoleStats)
                {
                    hole.hit_fairway = null;
                    hole.hit_green = null;
                }
                hole_stats.Add(EntityMetadataHelper.NewRecord(hole, username));
            }
            
            var rv = await _scorecardRepository.CreateNewScorecardAsync(round, holes, round_stats, hole_stats);
            return rv;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating a new scorecard.");
            return -1;
        }
    }

    public async Task<bool> UpdateScorecardAsync(ScorecardFormModel form)
    {
        // Validate round ID from the form
        if (form.RoundFormModel.RoundId is null)
        {
            _logger.LogError("Round Id was null when trying to update round.");
            return false;
        }

        var roundId = form.RoundFormModel.RoundId.Value;

        // Retrieve the round from the repository
        var round = await _scorecardRepository.GetRoundByIdAsync(roundId);
        if (round is null)
        {
            _logger.LogError("Round came back null with id {0}", roundId);
            return false;
        }

        // Fetch hole scores, round stats, and hole stats concurrently
        var holeScoresTask = _scorecardRepository.GetScoresForRoundByRoundIdAsync(roundId);
        var roundStatsTask = _scorecardRepository.GetRoundStatsByRoundIdAsync(roundId);
        var holeStatsTask = _scorecardRepository.GetHoleStatsByRoundAsync(roundId);

        await Task.WhenAll(holeScoresTask, roundStatsTask, holeStatsTask);

        var holeScores = await holeScoresTask;
        var roundStats = await roundStatsTask;
        var holeStats = await holeStatsTask;

        if (holeStats.Count <= 0 && form.RoundFormModel.UsingHoleStats)
        {
            var hole_stats = new List<HoleStats>();
            
            // Generate blank holestats to use
            holeStats.AddRange(holeScores.Select(hole => new HoleStats
            {
                score_id = hole.score_id, round_id = hole.round_id, hole_id = hole.hole_id,
            }).Select(hs => EntityMetadataHelper.NewRecord(hs, _usernameRetriever.Username)));

            await _scorecardRepository.InsertHoleStatsAsync(holeStats);

            holeStats = await _scorecardRepository.GetHoleStatsByRoundAsync(roundId);
        }

        // Update or create round stats based on the form's hole scores
        roundStats = roundStats is null 
            ? EntityMetadataHelper.NewRecord(GolfStatHelpers.GenerateRoundStats(form.HoleScore), _usernameRetriever.Username)
            : EntityMetadataHelper.UpdateRecord(roundStats.RefreshRoundStats(form.HoleScore), _usernameRetriever.Username);
        roundStats.round_id = roundId;

        // Update each hole score with values from the form
        var updatedHoleScores = holeScores.Select(score =>
        {
            var formScore = form.HoleScore.FirstOrDefault(h => h.HoleId == score.hole_id);
            if (formScore != null)
            {
                score.hole_score = formScore.Score;
                score.hole_number = formScore.HoleNumber;
                score = EntityMetadataHelper.UpdateRecord(score, _usernameRetriever.Username);
            }
            return score;
        }).ToList();

        
        // Update hole stats based on the form values
        var updatedHoleStats = new List<HoleStats>();
        if (form.RoundFormModel.UsingHoleStats)
        {
            updatedHoleStats = form.HoleScore.Select(formScore =>
            {
                var holeStat = holeStats.First(x => x.hole_id == formScore.HoleId);
            
                // If both Hit and Miss are false, leave the value as null. Otherwise, take the true value since both cant be true
                holeStat.hit_fairway = formScore.HoleStats.HitFairway ? true : formScore.HoleStats.MissedFairway ? false : null;
                holeStat.hit_green = formScore.HoleStats.HitGreen ? true : formScore.HoleStats.MissedGreen ? false : null;
            
                // If we hit, it should be null. If we miss it should have a value. If both are false, then it should be null.
                holeStat.miss_fairway_type = formScore.HoleStats.HitFairway || !formScore.HoleStats.MissedFairway ? null : formScore.HoleStats.MissFairwayType;
                holeStat.miss_green_type = formScore.HoleStats.HitGreen || !formScore.HoleStats.MissedGreen ? null : formScore.HoleStats.MissGreenType;

                holeStat.number_of_putts = formScore.HoleStats.NumberOfPutts;
                holeStat.approach_yardage = formScore.HoleStats.YardageOut;
            
                return EntityMetadataHelper.UpdateRecord(holeStat, _usernameRetriever.Username);
            }).ToList();
        }
        
        

        // Recalculate round scores from the updated hole scores
        round.score_out = updatedHoleScores.Where(x => x.hole_number <= 9).Sum(y => y.hole_score);
        round.score_in = updatedHoleScores.Where(x => x.hole_number > 9).Sum(y => y.hole_score);
        round.score = round.score_out + round.score_in;

        // Update round properties based on the form
        round.date_played = form.RoundFormModel.DatePlayed;
        round.using_hole_stats = form.RoundFormModel.UsingHoleStats;
        round = EntityMetadataHelper.UpdateRecord(round, _usernameRetriever.Username);

        // Persist the updated scorecard details
        return await _scorecardRepository.UpdateScorecardAsync(round, updatedHoleScores, roundStats, updatedHoleStats);
    }

}