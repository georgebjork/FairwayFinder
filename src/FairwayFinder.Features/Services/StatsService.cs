using System.Diagnostics;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Diagnostics;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Helpers;
using FairwayFinder.Features.Services.Interfaces;

namespace FairwayFinder.Features.Services;

public class StatsService : IStatsService
{
    private readonly IRoundService _roundService;

    public StatsService(IRoundService roundService)
    {
        _roundService = roundService;
    }

    public async Task<UserStatsResponse> GetUserStatsAsync(string userId, StatsFilter? filter = null, int coursesCount = 5, BaselineLevel level = BaselineLevel.Scratch)
    {
        using var activity = FairwayFinderDiagnostics.StatsActivity.StartActivity(name: FairwayFinderDiagnostics.ActivityNames.StatsUserGenerate);
        var stopwatch = Stopwatch.StartNew();
        var hasRounds = false;
        var hasSg = false;

        try
        {
            var rounds = await _roundService.GetRoundsWithDetailsAsync(userId, null, level);

            var statsRounds = rounds.Where(r => !r.ExcludeFromStats).ToList();

            // Apply filters
            statsRounds = ApplyFilters(statsRounds, filter);

            activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.StatsRoundCount, statsRounds.Count);
            activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.StatsFiltered, filter is not null);

            if (statsRounds.Count == 0)
            {
                return new UserStatsResponse();
            }
            hasRounds = true;

            // All aggregation below is in-memory CPU work over the already-fetched rounds.
            using var calcActivity = FairwayFinderDiagnostics.StatsActivity
                .StartActivity(FairwayFinderDiagnostics.ActivityNames.StatsUserCalculate);

            ScoringStats scoring;
            using (FairwayFinderDiagnostics.StatsActivity
                .StartActivity(FairwayFinderDiagnostics.ActivityNames.StatsCalcScoring))
            {
                scoring = new ScoringStats
                {
                    RoundsIncluded = statsRounds.Count,
                    Rounds18Hole = statsRounds.Count(x => x.FullRound),
                    Rounds9Hole = statsRounds.Count(x => !x.FullRound),
                    Average18HoleScore = StatsCalculator.CalculateAverageScore(statsRounds),
                    Average9HoleScore = StatsCalculator.CalculateAverageScore(statsRounds, false),
                    Average18HoleScoreTrend = StatsCalculator.CalculateScoreTrend(statsRounds),
                    Average9HoleScoreTrend = StatsCalculator.CalculateScoreTrend(statsRounds, fullRound: false),
                    Best18HoleRound = StatsCalculator.FindBestRound(statsRounds),
                    Best9HoleRound = StatsCalculator.FindBestRound(statsRounds, false),
                    ScoreTrend18Hole = StatsCalculator.BuildScoreTrend(statsRounds),
                    ScoreTrend9Hole = StatsCalculator.BuildScoreTrend(statsRounds, fullRound: false)
                };
            }

            // BallStriking/ShortGame start their own child spans inside the shared helpers.
            var response = new UserStatsResponse
            {
                TotalRounds = statsRounds.Count,
                Scoring = scoring,
                BallStriking = BuildBallStriking(statsRounds),
                ShortGame = BuildShortGame(statsRounds)
            };

            using (FairwayFinderDiagnostics.StatsActivity
                .StartActivity(FairwayFinderDiagnostics.ActivityNames.StatsCalcDistribution))
            {
                response.ParTypeScoring = StatsCalculator.CalculateParTypeScoring(statsRounds);
                response.ScoringDistribution = StatsCalculator.AggregateScoringDistribution(statsRounds);
                response.MostPlayedCourses = StatsCalculator.CalculateCourseStats(statsRounds, coursesCount);
            }

            // Strokes Gained — from stored values on RoundResponse
            var roundsWithSg = statsRounds.Where(r => r.StrokesGained != null).ToList();
            if (roundsWithSg.Count > 0)
            {
                hasSg = true;
                using (FairwayFinderDiagnostics.StatsActivity
                    .StartActivity(FairwayFinderDiagnostics.ActivityNames.StatsCalcStrokesGained))
                {
                    response.StrokesGained = new StrokesGainedStats
                    {
                        RoundsIncluded = roundsWithSg.Count,
                        Summary = AggregateStoredSg(roundsWithSg),
                        TotalTrend = BuildSgTrendFromStored(roundsWithSg, sg => sg.SgTotal),
                        PuttingTrend = BuildSgTrendFromStored(roundsWithSg, sg => sg.SgPutting),
                        TeeToGreenTrend = BuildSgTrendFromStored(roundsWithSg, sg => sg.SgTeeToGreen)
                    };
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            FairwayFinderDiagnostics.StatsDashDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new TagList
                {
                    { FairwayFinderDiagnostics.Tags.HasRounds, hasRounds },
                    { FairwayFinderDiagnostics.Tags.HasSg, hasSg }
                });
        }
    }

    public async Task<CourseStatsResponse?> GetCourseStatsAsync(string userId, long courseId, long? teeboxId = null, DateOnly? startDate = null, DateOnly? endDate = null, bool? fullRoundOnly = null, int? year = null, BaselineLevel level = BaselineLevel.Scratch)
    {
        using var activity = FairwayFinderDiagnostics.StatsActivity.StartActivity(name: FairwayFinderDiagnostics.ActivityNames.StatsCourseGenerate);
        var stopwatch = Stopwatch.StartNew();
        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.StatsCourseId, courseId);
        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.StatsTeeboxId, teeboxId);

        var result = FairwayFinderDiagnostics.TagValues.ResultError;
        var hasSg = false;

        try
        {
            var rounds = await _roundService.GetRoundsWithDetailsAsync(userId, null, level);

            var filter = FilterCourseRounds(rounds, courseId, teeboxId, startDate, endDate, year, fullRoundOnly);
            if (filter is null)
            {
                result = FairwayFinderDiagnostics.TagValues.ResultEmpty;
                return null;
            }

            if (filter.FilteredRounds.Count == 0)
            {
                result = FairwayFinderDiagnostics.TagValues.ResultFilteredEmpty;
                // Teebox/date/year/fullRound filter matched no rounds — return shell with options so user can pick another.
                return new CourseStatsResponse
                {
                    CourseId = courseId,
                    CourseName = filter.CourseName,
                    TeeboxOptions = filter.TeeboxOptions,
                    SelectedTeeboxId = teeboxId
                };
            }

            var filteredRounds = filter.FilteredRounds;
            var teeboxOptions = filter.TeeboxOptions;
            var courseName = filter.CourseName;

            using (FairwayFinderDiagnostics.StatsActivity
                .StartActivity(FairwayFinderDiagnostics.ActivityNames.StatsCourseCalculate))
            {
                var response = new CourseStatsResponse
                {
                    CourseId = courseId,
                    CourseName = courseName,
                    TotalRounds = filteredRounds.Count,
                    Scoring = new CourseScoringStats
                    {
                        RoundsIncluded = filteredRounds.Count,
                        Rounds18Hole = filteredRounds.Count(r => r.FullRound),
                        Rounds9Hole = filteredRounds.Count(r => !r.FullRound),
                        Average18HoleScore = StatsCalculator.CalculateAverageScore(filteredRounds),
                        Average9HoleScore = StatsCalculator.CalculateAverageScore(filteredRounds, false),
                        Best18HoleRound = StatsCalculator.FindBestRound(filteredRounds),
                        Best9HoleRound = StatsCalculator.FindBestRound(filteredRounds, false),
                        Average18HoleScoreTrend = StatsCalculator.CalculateScoreTrend(filteredRounds),
                        Average9HoleScoreTrend = StatsCalculator.CalculateScoreTrend(filteredRounds, fullRound: false),
                        ScoreTrend18Hole = StatsCalculator.BuildScoreTrend(filteredRounds, fullRound: true),
                        ScoreTrend9Hole = StatsCalculator.BuildScoreTrend(filteredRounds, fullRound: false)
                    },
                    BallStriking = BuildBallStriking(filteredRounds),
                    ShortGame = BuildShortGame(filteredRounds),
                    ParTypeScoring = StatsCalculator.CalculateParTypeScoring(filteredRounds),
                    HoleStats = StatsCalculator.CalculateHoleAggregateStats(filteredRounds),
                    ScoringDistribution = StatsCalculator.AggregateScoringDistribution(filteredRounds),
                    TeeboxOptions = teeboxOptions,
                    SelectedTeeboxId = teeboxId
                };

                // Strokes Gained from stored values
                var roundsWithSg = filteredRounds.Where(r => r.StrokesGained != null).ToList();
                if (roundsWithSg.Count > 0)
                {
                    hasSg = true;
                    response.StrokesGained = new CourseStrokesGainedStats
                    {
                        RoundsIncluded = roundsWithSg.Count,
                        Summary = AggregateStoredSg(roundsWithSg)
                    };
                }

                result = FairwayFinderDiagnostics.TagValues.ResultOk;
                return response;
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            FairwayFinderDiagnostics.StatsCourseDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new TagList
                {
                    { FairwayFinderDiagnostics.Tags.Result, result },
                    { FairwayFinderDiagnostics.Tags.HasSg, hasSg }
                });
        }
    }

    public async Task<CourseHoleStatsResponse?> GetCourseHoleStatsAsync(string userId, long courseId, long? teeboxId = null, DateOnly? startDate = null, DateOnly? endDate = null, bool? fullRoundOnly = null, int? year = null, BaselineLevel level = BaselineLevel.Scratch)
    {
        using var activity = FairwayFinderDiagnostics.StatsActivity.StartActivity(name: FairwayFinderDiagnostics.ActivityNames.StatsCourseHolesGenerate);
        var stopwatch = Stopwatch.StartNew();
        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.StatsCourseId, courseId);
        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.StatsTeeboxId, teeboxId);

        var result = FairwayFinderDiagnostics.TagValues.ResultError;
        var hasSg = false;

        try
        {
            var rounds = await _roundService.GetRoundsWithDetailsAsync(userId, null, level);

            var filter = FilterCourseRounds(rounds, courseId, teeboxId, startDate, endDate, year, fullRoundOnly);
            if (filter is null)
            {
                result = FairwayFinderDiagnostics.TagValues.ResultEmpty;
                return null;
            }

            if (filter.FilteredRounds.Count == 0)
            {
                result = FairwayFinderDiagnostics.TagValues.ResultFilteredEmpty;
                return new CourseHoleStatsResponse
                {
                    CourseId = courseId,
                    CourseName = filter.CourseName,
                    TeeboxOptions = filter.TeeboxOptions,
                    SelectedTeeboxId = teeboxId,
                    Holes = new List<HoleStatsDetail>()
                };
            }

            using (FairwayFinderDiagnostics.StatsActivity
                .StartActivity(FairwayFinderDiagnostics.ActivityNames.StatsCourseHolesCalculate))
            {
                var holes = BuildHoleStatsDetails(filter.FilteredRounds, out hasSg);

                result = FairwayFinderDiagnostics.TagValues.ResultOk;
                return new CourseHoleStatsResponse
                {
                    CourseId = courseId,
                    CourseName = filter.CourseName,
                    TotalRounds = filter.FilteredRounds.Count,
                    TeeboxOptions = filter.TeeboxOptions,
                    SelectedTeeboxId = teeboxId,
                    Holes = holes
                };
            }
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
        finally
        {
            FairwayFinderDiagnostics.StatsCourseHolesDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new TagList
                {
                    { FairwayFinderDiagnostics.Tags.Result, result },
                    { FairwayFinderDiagnostics.Tags.HasSg, hasSg }
                });
        }
    }

    public async Task<List<int>> GetAvailableYearsAsync(string userId)
    {
        var rounds = await _roundService.GetRoundsByUserIdAsync(userId);
        
        return rounds
            .Select(r => r.DatePlayed.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();
    }
    
    public async Task<List<CourseOption>> GetUserCoursesAsync(string userId)
    {
        var courses = await _roundService.GetPlayedCoursesByUserId(userId, true);

        return courses.Select(x => new CourseOption
        {
            CourseId = x.CourseId,
            CourseName = x.CourseName
        }).ToList();
    }
    
    private static List<RoundResponse> ApplyFilters(List<RoundResponse> rounds, StatsFilter? filter)
    {
        if (filter is null || !filter.HasFilters)
        {
            return rounds;
        }
        
        var result = rounds.AsEnumerable();
        
        // Filter by round type (9 or 18 hole)
        if (filter.FullRoundOnly.HasValue)
        {
            result = result.Where(r => r.FullRound == filter.FullRoundOnly.Value);
        }
        
        // Filter by date range
        if (filter.StartDate.HasValue)
        {
            result = result.Where(r => r.DatePlayed >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            result = result.Where(r => r.DatePlayed <= filter.EndDate.Value);
        }

        // Filter by year — only when no explicit date range was given.
        if (filter.Year.HasValue && !filter.StartDate.HasValue && !filter.EndDate.HasValue)
        {
            result = result.Where(r => r.DatePlayed.Year == filter.Year.Value);
        }

        // Filter by course
        if (filter.CourseId.HasValue)
        {
            result = result.Where(r => r.CourseId == filter.CourseId.Value);
        }

        return result.ToList();
    }

    /// <summary>
    /// Aggregates stored SG values from multiple rounds into an average summary.
    /// </summary>
    private static StrokesGainedSummary AggregateStoredSg(List<RoundResponse> rounds)
    {
        var count = rounds.Count;
        var summary = new StrokesGainedSummary
        {
            RoundsIncluded = count,
            HolesWithShots = rounds.Sum(r => r.StrokesGained!.HolesWithShots),
            SgTotal = Math.Round(rounds.Average(r => r.StrokesGained!.SgTotal), 2),
            SgPutting = Math.Round(rounds.Average(r => r.StrokesGained!.SgPutting), 2),
            SgTeeToGreen = Math.Round(rounds.Average(r => r.StrokesGained!.SgTeeToGreen), 2),
            SgOffTheTee = Math.Round(rounds.Average(r => r.StrokesGained!.SgOffTheTee), 2),
            SgApproach = Math.Round(rounds.Average(r => r.StrokesGained!.SgApproach), 2),
            SgAroundTheGreen = Math.Round(rounds.Average(r => r.StrokesGained!.SgAroundTheGreen), 2)
        };

        if (count >= 3)
        {
            var ordered = rounds.OrderBy(r => r.DatePlayed).Select(r => r.StrokesGained!).ToList();
            summary.SgTotalTrend = CalcSlope(ordered.Select(s => s.SgTotal).ToList());
            summary.SgPuttingTrend = CalcSlope(ordered.Select(s => s.SgPutting).ToList());
            summary.SgTeeToGreenTrend = CalcSlope(ordered.Select(s => s.SgTeeToGreen).ToList());
            summary.SgOffTheTeeTrend = CalcSlope(ordered.Select(s => s.SgOffTheTee).ToList());
            summary.SgApproachTrend = CalcSlope(ordered.Select(s => s.SgApproach).ToList());
            summary.SgAroundTheGreenTrend = CalcSlope(ordered.Select(s => s.SgAroundTheGreen).ToList());
        }

        return summary;
    }

    /// <summary>
    /// Builds the ball-striking group (FIR/GIR + per-round trend chart data) for a set of rounds.
    /// Shared by user-wide and course-scoped stats.
    /// </summary>
    private static BallStrikingStats BuildBallStriking(IReadOnlyList<RoundResponse> rounds)
    {
        using var activity = FairwayFinderDiagnostics.StatsActivity
            .StartActivity(FairwayFinderDiagnostics.ActivityNames.StatsCalcBallStriking);

        var ballStriking = StatsCalculator.CalculateBallStriking(rounds);
        ballStriking.FirTrend = StatsCalculator.BuildFirTrend(rounds);
        ballStriking.GirTrend = StatsCalculator.BuildGirTrend(rounds);
        return ballStriking;
    }

    /// <summary>
    /// Builds the short-game group (putting/3-putts/up-and-down + per-round trend chart data)
    /// for a set of rounds. Shared by user-wide and course-scoped stats.
    /// </summary>
    private static ShortGameStats BuildShortGame(IReadOnlyList<RoundResponse> rounds)
    {
        using var activity = FairwayFinderDiagnostics.StatsActivity
            .StartActivity(FairwayFinderDiagnostics.ActivityNames.StatsCalcShortGame);

        var shortGame = StatsCalculator.CalculateShortGame(rounds);
        shortGame.PuttsTrend18Hole = StatsCalculator.BuildPuttsTrend(rounds);
        shortGame.PuttsTrend9Hole = StatsCalculator.BuildPuttsTrend(rounds, fullRound: false);
        shortGame.ThreePuttsTrend18Hole = StatsCalculator.BuildThreePuttsTrend(rounds);
        shortGame.ThreePuttsTrend9Hole = StatsCalculator.BuildThreePuttsTrend(rounds, fullRound: false);
        shortGame.UpAndDownTrend = StatsCalculator.BuildUpAndDownTrend(rounds);
        return shortGame;
    }

    /// <summary>
    /// Builds SG trend points from stored per-round values.
    /// </summary>
    private static List<StrokesGainedTrendPoint> BuildSgTrendFromStored(
        List<RoundResponse> rounds, Func<StrokesGainedSummary, double> valueSelector)
    {
        var points = rounds
            .OrderBy(r => r.DatePlayed)
            .Select(r => new StrokesGainedTrendPoint
            {
                RoundId = r.RoundId,
                DatePlayed = r.DatePlayed,
                CourseName = r.CourseName,
                Value = Math.Round(valueSelector(r.StrokesGained!), 2)
            })
            .ToList();

        // 3-round moving average
        for (int i = 2; i < points.Count; i++)
        {
            points[i].MovingAverage = Math.Round(
                (points[i].Value + points[i - 1].Value + points[i - 2].Value) / 3.0, 2);
        }

        return points;
    }

    private static double? CalcSlope(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return null;
        var (slope, _) = StatsCalculator.CalculateLinearRegression(values);
        return Math.Round(slope, 3);
    }

    /// <summary>
    /// Result of filtering a user's rounds down to a single course + the user's
    /// optional filter criteria. <see cref="TeeboxOptions"/> is always built from
    /// the pre-filter set so the dropdown stays stable when filters eliminate
    /// every round.
    /// </summary>
    private sealed record FilteredCourseRounds(
        List<RoundResponse> FilteredRounds,
        List<CourseTeeboxOption> TeeboxOptions,
        string CourseName);

    /// <summary>
    /// Filters a user's rounds down to a single course and applies the standard
    /// teebox / date / year / fullRound filter pipeline. Returns null when the
    /// user has no rounds at this course at all (caller should 404). Returns an
    /// instance with empty <see cref="FilteredCourseRounds.FilteredRounds"/>
    /// when the filters eliminate every round (caller returns a shell response
    /// with the teebox options populated so the UI can adjust).
    /// </summary>
    private static FilteredCourseRounds? FilterCourseRounds(
        IReadOnlyList<RoundResponse> rounds,
        long courseId,
        long? teeboxId,
        DateOnly? startDate,
        DateOnly? endDate,
        int? year,
        bool? fullRoundOnly)
    {
        var allCourseRounds = rounds
            .Where(r => r.CourseId == courseId && !r.ExcludeFromStats)
            .ToList();

        if (allCourseRounds.Count == 0) return null;

        // Build teebox options from all rounds at this course (before any filtering).
        // Group by lineage key so all versions of a re-rated tee collapse into one option;
        // the option value is the group id and the label is the group's most-used name.
        var teeboxOptions = allCourseRounds
            .GroupBy(r => r.Teebox.TeeboxGroupId)
            .Select(g => new CourseTeeboxOption
            {
                TeeboxId = g.Key,
                TeeboxName = g
                    .GroupBy(r => r.Teebox.TeeboxName)
                    .OrderByDescending(n => n.Count())
                    .First().Key,
                RoundCount = g.Count()
            })
            .OrderByDescending(o => o.RoundCount)
            .ToList();

        // Apply teebox filter if specified (incoming value is a lineage group id)
        var filteredRounds = teeboxId.HasValue
            ? allCourseRounds.Where(r => r.Teebox.TeeboxGroupId == teeboxId.Value).ToList()
            : allCourseRounds.ToList();

        // Apply date range filter
        if (startDate.HasValue)
        {
            filteredRounds = filteredRounds
                .Where(r => r.DatePlayed >= startDate.Value)
                .ToList();
        }
        if (endDate.HasValue)
        {
            filteredRounds = filteredRounds
                .Where(r => r.DatePlayed <= endDate.Value)
                .ToList();
        }

        // Apply year filter — only when no explicit date range was given.
        if (year.HasValue && !startDate.HasValue && !endDate.HasValue)
        {
            filteredRounds = filteredRounds
                .Where(r => r.DatePlayed.Year == year.Value)
                .ToList();
        }

        // Apply round-type filter (18-hole vs 9-hole)
        if (fullRoundOnly.HasValue)
        {
            filteredRounds = filteredRounds
                .Where(r => r.FullRound == fullRoundOnly.Value)
                .ToList();
        }

        return new FilteredCourseRounds(
            FilteredRounds: filteredRounds,
            TeeboxOptions: teeboxOptions,
            CourseName: allCourseRounds[0].CourseName);
    }

    /// <summary>
    /// Composes per-hole detail entries: existing aggregate row + per-hole scoring
    /// distribution + plays list (newest first) + averaged strokes-gained block.
    /// Only holes with at least one play in <paramref name="filteredRounds"/> are
    /// included; this matches <see cref="StatsCalculator.CalculateHoleAggregateStats"/>'s
    /// own behavior.
    /// </summary>
    private static List<HoleStatsDetail> BuildHoleStatsDetails(
        IReadOnlyList<RoundResponse> filteredRounds, out bool anyHoleHasSg)
    {
        var aggregates = StatsCalculator.CalculateHoleAggregateStats(filteredRounds);

        // Group plays by hole number, ordered newest first for the iOS list view.
        var playsByHole = filteredRounds
            .SelectMany(r => r.Holes
                .Where(h => h.Score.HasValue)
                .Select(h => new
                {
                    h.HoleNumber,
                    Play = new HolePlay
                    {
                        RoundId = r.RoundId,
                        DatePlayed = r.DatePlayed,
                        Score = h.Score!.Value,
                        ScoreToPar = h.Score!.Value - h.Par,
                        TeeboxName = r.Teebox.TeeboxName
                    }
                }))
            .GroupBy(x => x.HoleNumber)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Play).OrderByDescending(p => p.DatePlayed).ToList());

        // Group per-hole SG records by hole number (rounds without shot tracking are skipped).
        var sgByHole = filteredRounds
            .Where(r => r.HoleByHoleSg is { Count: > 0 })
            .SelectMany(r => r.HoleByHoleSg!)
            .GroupBy(sg => sg.HoleNumber)
            .ToDictionary(g => g.Key, g => g.ToList());

        anyHoleHasSg = sgByHole.Count > 0;

        var details = new List<HoleStatsDetail>(aggregates.Count);
        foreach (var agg in aggregates)
        {
            var plays = playsByHole.TryGetValue(agg.HoleNumber, out var ps) ? ps : new List<HolePlay>();
            var distribution = StatsCalculator.CalculateHoleScoringDistribution(plays, agg.Par);

            // Ascending so the slope goes old→new (negative = scores trending down over time).
            var scoreTrend = CalcSlope(plays.OrderBy(p => p.DatePlayed).Select(p => (double)p.Score).ToList());

            HoleAverageSg? avgSg = null;
            if (sgByHole.TryGetValue(agg.HoleNumber, out var sgList) && sgList.Count > 0)
            {
                avgSg = StrokesGainedCalculator.AverageHoleSg(sgList);
            }

            details.Add(new HoleStatsDetail
            {
                // Aggregate fields (inherited)
                HoleNumber                       = agg.HoleNumber,
                Par                              = agg.Par,
                Handicap                         = agg.Handicap,
                AverageYardage                   = agg.AverageYardage,
                TimesPlayed                      = agg.TimesPlayed,
                AverageScore                     = agg.AverageScore,
                AverageScoreToPar                = agg.AverageScoreToPar,
                FairwayHitPercent                = agg.FairwayHitPercent,
                GirPercent                       = agg.GirPercent,
                AveragePutts                     = agg.AveragePutts,
                TeeShotOutOfPositionPercent      = agg.TeeShotOutOfPositionPercent,
                ApproachShotOutOfPositionPercent = agg.ApproachShotOutOfPositionPercent,
                FairwayMiss                      = agg.FairwayMiss,
                GreenMiss                        = agg.GreenMiss,
                // Per-hole-detail fields
                ScoringDistribution              = distribution,
                Plays                            = plays,
                StrokesGained                    = avgSg,
                AverageScoreTrend                = scoreTrend
            });
        }
        return details;
    }
}
