using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.QueryModels;
using FairwayFinder.Core.Features.Scorecards.Repositories;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Services;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Scorecards.Services;

public class ScorecardService 
{
    private readonly ILogger<ScorecardService> _logger;
    private readonly IScorecardRepository _scorecardRepository;
    private readonly CourseLookupService _courseLookupService;
    private readonly TeeboxLookupService _teeboxLookupService;
    private readonly HoleLookupService _holeLookupService;
    private readonly IUsernameRetriever _usernameRetriever;

    public ScorecardService(ILogger<ScorecardService> logger, IScorecardRepository scorecardRepository, TeeboxLookupService teeboxLookupService, CourseLookupService courseLookupService, IUsernameRetriever usernameRetriever, HoleLookupService holeLookupService)
    {
        _logger = logger;
        _scorecardRepository = scorecardRepository;
        _teeboxLookupService = teeboxLookupService;
        _courseLookupService = courseLookupService;
        _usernameRetriever = usernameRetriever;
        _holeLookupService = holeLookupService;
    }

    public async Task<List<ScorecardSummaryQueryModel>> GetScorecardSummaryByUserIdAsync(string username)
    {
        return await _scorecardRepository.GetScorecardSummaryByUserIdAsync(username);
    }
    
    public async Task<ScorecardSummaryQueryModel?> GetScorecardSummaryByRoundIdAsync(long roundId)
    {
        return await _scorecardRepository.GetScorecardSummaryByRoundIdAsync(roundId);
    }

    public async Task<List<HoleScoreQueryModel>> GetScorecardHoleScoresByRoundIdAsync(long roundId)
    {
        return await _scorecardRepository.GetScorecardHoleScoresByRoundIdAsync(roundId);
    }
    
    public async Task<Round?> GetScorecardByIdAsync(long roundId)
    {
        return await _scorecardRepository.GetScorecardByIdAsync(roundId);
    }
    
    public async Task<List<HoleScoreFormModel>> GetHoleScoreFormsByRoundIdAsync(long roundId)
    {
        var holes = await _holeLookupService.GetHolesForRoundByRoundIdAsync(roundId);
        var scores = await _scorecardRepository.GetScorecardHoleScoresByRoundIdAsync(roundId);
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

    public async Task<int> CreateNewScorecardAsync(ScorecardFormModel form)
    {
        // Validate TeeboxId is a valid integer
        if (!int.TryParse(form.TeeboxId, out int teeboxId))
        {
            _logger.LogError("Invalid TeeboxId format: {TeeboxId}", form.TeeboxId);
            return -1;
        }

        // Validate that the course exists
        var course = await _courseLookupService.GetCourseByIdAsync(form.CourseId);
        if (course == null)
        {
            _logger.LogError("Course not found for CourseId: {CourseId}", form.CourseId);
            return -1;
        }

        // Validate that the teebox exists
        var teebox = await _teeboxLookupService.GetTeeByIdAsync(teeboxId);
        if (teebox == null)
        {
            _logger.LogError("Teebox not found for TeeboxId: {TeeboxId}", teeboxId);
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
                course_id = form.CourseId,
                teebox_id = int.Parse(form.TeeboxId),
                date_played = form.DatePlayed,
                user_id = user_id,
                score = form.HoleScore.Sum(x => x.Score),
                score_out = form.HoleScore.Where(x => x.HoleNumber <= 9).Sum(x => x.Score),
                score_in = form.HoleScore.Where(x => x.HoleNumber > 9).Sum(x => x.Score)
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
            
            var rv = await _scorecardRepository.CreateNewScorecardAsync(round, holes, round_stats);
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
        if (form.RoundId is null)
        {
            _logger.LogError("Round Id was null when trying to update round.");
            return false;
        }

        var roundId = form.RoundId.Value;
        
        var round = await _scorecardRepository.GetRoundByIdAsync(roundId);
        if (round is null)
        {
            _logger.LogError("Round came back null with id {0}", roundId);
            return false;
        }


        // Retrieve hole scores and round stats
        var hole_scores = await _scorecardRepository.GetScoresForRoundByRoundIdAsync(roundId);
        var round_stats = await _scorecardRepository.GetRoundStatsForRoundAsync(roundId);
        
        
        // Initialize or update round stats
        round_stats = round_stats is null
            ? EntityMetadataHelper.NewRecord(GolfStatHelpers.GenerateRoundStats(form.HoleScore), _usernameRetriever.Username)
            : EntityMetadataHelper.UpdateRecord(round_stats.RefreshRoundStats(form.HoleScore), _usernameRetriever.Username);
        round_stats.round_id = roundId;
        
        // Update hole_scores with scores from form.HoleScores
        var updated_hole_scores = hole_scores.Select(score =>
        {
            var matchingScore = form.HoleScore.FirstOrDefault(h => h.HoleId == score.hole_id);
            if (matchingScore != null)
            {
                score.hole_score = matchingScore.Score;
                score.hole_number = matchingScore.HoleNumber;
                return EntityMetadataHelper.UpdateRecord(score, _usernameRetriever.Username);
            }
            return score;
        }).ToList();

        round.score_out = updated_hole_scores.Where(x => x.hole_number <= 9).Sum(y => y.hole_score);
        round.score_in = updated_hole_scores.Where(x => x.hole_number > 9).Sum(y => y.hole_score);
        round.score = round.score_out + round.score_in;
        round.date_played = form.DatePlayed;
        round = EntityMetadataHelper.UpdateRecord(round, _usernameRetriever.Username);
        
        return await _scorecardRepository.UpdateScorecardAsync(round, updated_hole_scores, round_stats);
    }
}