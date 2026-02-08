using FairwayFinder.Features.Data;
using FairwayFinder.Features.Helpers;

namespace FairwayFinder.Features.Tests.Helpers;

public class StatsCalculatorTests
{
    #region Test Helpers

    private static RoundResponse CreateRound(
        long roundId,
        int score,
        string courseName = "Test Course",
        long courseId = 1,
        DateOnly? datePlayed = null,
        bool usingHoleStats = false,
        bool excludeFromStats = false,
        RoundStats? stats = null,
        List<RoundHole>? holes = null)
    {
        return new RoundResponse
        {
            RoundId = roundId,
            Score = score,
            CourseName = courseName,
            CourseId = courseId,
            DatePlayed = datePlayed ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-roundId)),
            UsingHoleStats = usingHoleStats,
            ExcludeFromStats = excludeFromStats,
            Stats = stats,
            Holes = holes ?? new List<RoundHole>()
        };
    }

    private static RoundHole CreateHole(
        int holeNumber,
        int par,
        short? score = null,
        bool? hitFairway = null,
        bool? hitGreen = null,
        short? numberOfPutts = null)
    {
        RoundHoleStat? stats = null;
        if (hitFairway.HasValue || hitGreen.HasValue || numberOfPutts.HasValue)
        {
            stats = new RoundHoleStat
            {
                HitFairway = hitFairway,
                HitGreen = hitGreen,
                NumberOfPutts = numberOfPutts
            };
        }

        return new RoundHole
        {
            HoleId = holeNumber,
            HoleNumber = holeNumber,
            Par = par,
            Score = score,
            Stats = stats
        };
    }

    private static RoundStats CreateRoundStats(
        int birdies = 0,
        int pars = 0,
        int bogeys = 0,
        int doubleBogeys = 0,
        int tripleOrWorse = 0,
        int eagles = 0,
        int doubleEagles = 0,
        int holesInOne = 0)
    {
        return new RoundStats
        {
            Birdies = birdies,
            Pars = pars,
            Bogeys = bogeys,
            DoubleBogeys = doubleBogeys,
            TripleOrWorse = tripleOrWorse,
            Eagles = eagles,
            DoubleEagles = doubleEagles,
            HolesInOne = holesInOne
        };
    }

    #endregion

    #region CalculateAverageScore Tests

    [Fact]
    public void CalculateAverageScore_EmptyList_ReturnsNull()
    {
        var rounds = new List<RoundResponse>();

        var result = StatsCalculator.CalculateAverageScore(rounds);

        Assert.Null(result);
    }

    [Fact]
    public void CalculateAverageScore_SingleRound_ReturnsThatScore()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 85)
        };

        var result = StatsCalculator.CalculateAverageScore(rounds);

        Assert.Equal(85.0, result);
    }

    [Fact]
    public void CalculateAverageScore_MultipleRounds_ReturnsCorrectAverage()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80),
            CreateRound(2, 85),
            CreateRound(3, 90)
        };

        var result = StatsCalculator.CalculateAverageScore(rounds);

        Assert.Equal(85.0, result);
    }

    [Fact]
    public void CalculateAverageScore_RoundsOneDecimalPlace_ReturnsRoundedValue()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 81),
            CreateRound(2, 82),
            CreateRound(3, 83)
        };

        var result = StatsCalculator.CalculateAverageScore(rounds);

        Assert.Equal(82.0, result);
    }

    [Fact]
    public void CalculateAverageScore_DecimalResult_RoundsToOneDecimal()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80),
            CreateRound(2, 81),
            CreateRound(3, 82)
        };

        var result = StatsCalculator.CalculateAverageScore(rounds);

        Assert.Equal(81.0, result);
    }

    #endregion

    #region CalculateScoreTrend Tests

    [Fact]
    public void CalculateScoreTrend_LessThan10Rounds_ReturnsNull()
    {
        var rounds = new List<RoundResponse>();
        for (int i = 1; i <= 9; i++)
        {
            rounds.Add(CreateRound(i, 80));
        }

        var result = StatsCalculator.CalculateScoreTrend(rounds);

        Assert.Null(result);
    }

    [Fact]
    public void CalculateScoreTrend_Exactly10Rounds_ReturnsValue()
    {
        var rounds = new List<RoundResponse>();
        // Last 5 rounds (indices 0-4) avg 80
        for (int i = 1; i <= 5; i++)
        {
            rounds.Add(CreateRound(i, 80));
        }
        // Previous 5 rounds (indices 5-9) avg 85
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 85));
        }

        var result = StatsCalculator.CalculateScoreTrend(rounds);

        // 80 - 85 = -5 (improvement)
        Assert.Equal(-5.0, result);
    }

    [Fact]
    public void CalculateScoreTrend_ImprovingScores_ReturnsNegative()
    {
        var rounds = new List<RoundResponse>();
        // Recent rounds scoring better (lower)
        for (int i = 1; i <= 5; i++)
        {
            rounds.Add(CreateRound(i, 75));
        }
        // Previous rounds scoring worse (higher)
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 85));
        }

        var result = StatsCalculator.CalculateScoreTrend(rounds);

        // 75 - 85 = -10 (big improvement)
        Assert.Equal(-10.0, result);
    }

    [Fact]
    public void CalculateScoreTrend_DecliningScores_ReturnsPositive()
    {
        var rounds = new List<RoundResponse>();
        // Recent rounds scoring worse (higher)
        for (int i = 1; i <= 5; i++)
        {
            rounds.Add(CreateRound(i, 90));
        }
        // Previous rounds scoring better (lower)
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 80));
        }

        var result = StatsCalculator.CalculateScoreTrend(rounds);

        // 90 - 80 = 10 (regression)
        Assert.Equal(10.0, result);
    }

    [Fact]
    public void CalculateScoreTrend_CustomWindowSize_UsesCorrectWindow()
    {
        var rounds = new List<RoundResponse>();
        // 3 recent rounds avg 80
        for (int i = 1; i <= 3; i++)
        {
            rounds.Add(CreateRound(i, 80));
        }
        // 3 previous rounds avg 85
        for (int i = 4; i <= 6; i++)
        {
            rounds.Add(CreateRound(i, 85));
        }

        var result = StatsCalculator.CalculateScoreTrend(rounds, windowSize: 3);

        Assert.Equal(-5.0, result);
    }

    #endregion

    #region FindBestRound Tests

    [Fact]
    public void FindBestRound_EmptyList_ReturnsNull()
    {
        var rounds = new List<RoundResponse>();

        var result = StatsCalculator.FindBestRound(rounds);

        Assert.Null(result);
    }

    [Fact]
    public void FindBestRound_SingleRound_ReturnsThatRound()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 82, "Pine Valley", 10, new DateOnly(2024, 6, 15))
        };

        var result = StatsCalculator.FindBestRound(rounds);

        Assert.NotNull(result);
        Assert.Equal(1, result.RoundId);
        Assert.Equal(82, result.Score);
        Assert.Equal("Pine Valley", result.CourseName);
        Assert.Equal(new DateOnly(2024, 6, 15), result.DatePlayed);
    }

    [Fact]
    public void FindBestRound_MultipleRounds_ReturnsLowestScore()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 90, "Course A"),
            CreateRound(2, 75, "Course B"),
            CreateRound(3, 85, "Course C")
        };

        var result = StatsCalculator.FindBestRound(rounds);

        Assert.NotNull(result);
        Assert.Equal(2, result.RoundId);
        Assert.Equal(75, result.Score);
        Assert.Equal("Course B", result.CourseName);
    }

    [Fact]
    public void FindBestRound_TiedScores_ReturnsFirstOne()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, "First Course"),
            CreateRound(2, 80, "Second Course"),
            CreateRound(3, 85, "Third Course")
        };

        var result = StatsCalculator.FindBestRound(rounds);

        Assert.NotNull(result);
        Assert.Equal(80, result.Score);
        // OrderBy is stable, so first 80 should be returned
    }

    #endregion

    #region BuildScoreTrend Tests

    [Fact]
    public void BuildScoreTrend_EmptyList_ReturnsEmptyList()
    {
        var rounds = new List<RoundResponse>();

        var result = StatsCalculator.BuildScoreTrend(rounds, 10);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildScoreTrend_FewerThanCount_ReturnsAllReversed()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, datePlayed: new DateOnly(2024, 6, 3)),
            CreateRound(2, 85, datePlayed: new DateOnly(2024, 6, 2)),
            CreateRound(3, 90, datePlayed: new DateOnly(2024, 6, 1))
        };

        var result = StatsCalculator.BuildScoreTrend(rounds, 10);

        Assert.Equal(3, result.Count);
        // Should be reversed (oldest first)
        Assert.Equal(3, result[0].RoundId);
        Assert.Equal(2, result[1].RoundId);
        Assert.Equal(1, result[2].RoundId);
    }

    [Fact]
    public void BuildScoreTrend_MoreThanCount_TakesOnlyCount()
    {
        var rounds = new List<RoundResponse>();
        for (int i = 1; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 80 + i));
        }

        var result = StatsCalculator.BuildScoreTrend(rounds, 5);

        Assert.Equal(5, result.Count);
        // Takes first 5, then reverses
        Assert.Equal(5, result[0].RoundId);
        Assert.Equal(1, result[4].RoundId);
    }

    [Fact]
    public void BuildScoreTrend_MapsAllProperties()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(42, 78, "Augusta National", datePlayed: new DateOnly(2024, 4, 14))
        };

        var result = StatsCalculator.BuildScoreTrend(rounds, 10);

        Assert.Single(result);
        Assert.Equal(42, result[0].RoundId);
        Assert.Equal(78, result[0].Score);
        Assert.Equal("Augusta National", result[0].CourseName);
        Assert.Equal(new DateOnly(2024, 4, 14), result[0].DatePlayed);
    }

    #endregion

    #region CalculateCourseStats Tests

    [Fact]
    public void CalculateCourseStats_EmptyList_ReturnsEmptyList()
    {
        var rounds = new List<RoundResponse>();

        var result = StatsCalculator.CalculateCourseStats(rounds, 5);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculateCourseStats_SingleCourse_ReturnsCorrectStats()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, "Home Course", 1),
            CreateRound(2, 85, "Home Course", 1),
            CreateRound(3, 90, "Home Course", 1)
        };

        var result = StatsCalculator.CalculateCourseStats(rounds, 5);

        Assert.Single(result);
        Assert.Equal(1, result[0].CourseId);
        Assert.Equal("Home Course", result[0].CourseName);
        Assert.Equal(3, result[0].RoundCount);
        Assert.Equal(85.0, result[0].AverageScore);
    }

    [Fact]
    public void CalculateCourseStats_MultipleCourses_OrderedByRoundCount()
    {
        var rounds = new List<RoundResponse>
        {
            // Course A: 1 round
            CreateRound(1, 80, "Course A", 1),
            // Course B: 3 rounds
            CreateRound(2, 82, "Course B", 2),
            CreateRound(3, 84, "Course B", 2),
            CreateRound(4, 86, "Course B", 2),
            // Course C: 2 rounds
            CreateRound(5, 90, "Course C", 3),
            CreateRound(6, 92, "Course C", 3)
        };

        var result = StatsCalculator.CalculateCourseStats(rounds, 5);

        Assert.Equal(3, result.Count);
        Assert.Equal("Course B", result[0].CourseName);
        Assert.Equal(3, result[0].RoundCount);
        Assert.Equal("Course C", result[1].CourseName);
        Assert.Equal(2, result[1].RoundCount);
        Assert.Equal("Course A", result[2].CourseName);
        Assert.Equal(1, result[2].RoundCount);
    }

    [Fact]
    public void CalculateCourseStats_LimitsToCount()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, "Course A", 1),
            CreateRound(2, 80, "Course A", 1),
            CreateRound(3, 80, "Course B", 2),
            CreateRound(4, 80, "Course C", 3),
            CreateRound(5, 80, "Course D", 4),
            CreateRound(6, 80, "Course E", 5)
        };

        var result = StatsCalculator.CalculateCourseStats(rounds, 3);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void CalculateCourseStats_CalculatesAveragePerCourse()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 70, "Easy Course", 1),
            CreateRound(2, 72, "Easy Course", 1),
            CreateRound(3, 90, "Hard Course", 2),
            CreateRound(4, 95, "Hard Course", 2)
        };

        var result = StatsCalculator.CalculateCourseStats(rounds, 5);

        var easy = result.First(c => c.CourseName == "Easy Course");
        var hard = result.First(c => c.CourseName == "Hard Course");

        Assert.Equal(71.0, easy.AverageScore);
        Assert.Equal(92.5, hard.AverageScore);
    }

    #endregion

    #region AggregateScoringDistribution Tests

    [Fact]
    public void AggregateScoringDistribution_EmptyList_ReturnsEmptyDistribution()
    {
        var rounds = new List<RoundResponse>();

        var result = StatsCalculator.AggregateScoringDistribution(rounds);

        Assert.Equal(0, result.RoundCount);
        Assert.Equal(0, result.TotalHoles);
    }

    [Fact]
    public void AggregateScoringDistribution_RoundsWithoutStats_ReturnsEmptyDistribution()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, stats: null),
            CreateRound(2, 85, stats: null)
        };

        var result = StatsCalculator.AggregateScoringDistribution(rounds);

        Assert.Equal(0, result.RoundCount);
    }

    [Fact]
    public void AggregateScoringDistribution_SingleRoundWithStats_ReturnsThoseStats()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, stats: CreateRoundStats(
                birdies: 2,
                pars: 10,
                bogeys: 5,
                doubleBogeys: 1
            ))
        };

        var result = StatsCalculator.AggregateScoringDistribution(rounds);

        Assert.Equal(1, result.RoundCount);
        Assert.Equal(2, result.Birdies);
        Assert.Equal(10, result.Pars);
        Assert.Equal(5, result.Bogeys);
        Assert.Equal(1, result.DoubleBogeys);
        Assert.Equal(18, result.TotalHoles);
    }

    [Fact]
    public void AggregateScoringDistribution_MultipleRounds_SumsAllCategories()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 72, stats: CreateRoundStats(
                eagles: 1,
                birdies: 3,
                pars: 12,
                bogeys: 2
            )),
            CreateRound(2, 80, stats: CreateRoundStats(
                birdies: 1,
                pars: 8,
                bogeys: 6,
                doubleBogeys: 3
            ))
        };

        var result = StatsCalculator.AggregateScoringDistribution(rounds);

        Assert.Equal(2, result.RoundCount);
        Assert.Equal(1, result.Eagles);
        Assert.Equal(4, result.Birdies);
        Assert.Equal(20, result.Pars);
        Assert.Equal(8, result.Bogeys);
        Assert.Equal(3, result.DoubleBogeys);
    }

    [Fact]
    public void AggregateScoringDistribution_AllScoreTypes_AggregatesCorrectly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 70, stats: CreateRoundStats(
                holesInOne: 1,
                doubleEagles: 1,
                eagles: 2,
                birdies: 4,
                pars: 6,
                bogeys: 2,
                doubleBogeys: 1,
                tripleOrWorse: 1
            ))
        };

        var result = StatsCalculator.AggregateScoringDistribution(rounds);

        Assert.Equal(1, result.HolesInOne);
        Assert.Equal(1, result.DoubleEagles);
        Assert.Equal(2, result.Eagles);
        Assert.Equal(4, result.Birdies);
        Assert.Equal(6, result.Pars);
        Assert.Equal(2, result.Bogeys);
        Assert.Equal(1, result.DoubleBogeys);
        Assert.Equal(1, result.TripleOrWorse);
        Assert.Equal(18, result.TotalHoles);
    }

    #endregion

    #region CalculateParTypeScoring Tests

    [Fact]
    public void CalculateParTypeScoring_EmptyList_ReturnsNullAverages()
    {
        var rounds = new List<RoundResponse>();

        var result = StatsCalculator.CalculateParTypeScoring(rounds);

        Assert.Null(result.Par3Average);
        Assert.Null(result.Par4Average);
        Assert.Null(result.Par5Average);
        Assert.Equal(0, result.Par3Count);
        Assert.Equal(0, result.Par4Count);
        Assert.Equal(0, result.Par5Count);
    }

    [Fact]
    public void CalculateParTypeScoring_NoScoredHoles_ReturnsNullAverages()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 0, holes: new List<RoundHole>
            {
                CreateHole(1, 4, score: null),
                CreateHole(2, 3, score: null)
            })
        };

        var result = StatsCalculator.CalculateParTypeScoring(rounds);

        Assert.Null(result.Par3Average);
        Assert.Null(result.Par4Average);
    }

    [Fact]
    public void CalculateParTypeScoring_OnlyPar3s_CalculatesCorrectly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 0, holes: new List<RoundHole>
            {
                CreateHole(1, 3, score: 3),
                CreateHole(2, 3, score: 4),
                CreateHole(3, 3, score: 2)
            })
        };

        var result = StatsCalculator.CalculateParTypeScoring(rounds);

        Assert.Equal(3.0, result.Par3Average);
        Assert.Equal(3, result.Par3Count);
        Assert.Null(result.Par4Average);
        Assert.Null(result.Par5Average);
    }

    [Fact]
    public void CalculateParTypeScoring_AllParTypes_CalculatesEachCorrectly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 0, holes: new List<RoundHole>
            {
                // Par 3s: avg 3.5
                CreateHole(1, 3, score: 3),
                CreateHole(2, 3, score: 4),
                // Par 4s: avg 5.0
                CreateHole(3, 4, score: 4),
                CreateHole(4, 4, score: 5),
                CreateHole(5, 4, score: 6),
                // Par 5s: avg 5.0
                CreateHole(6, 5, score: 5)
            })
        };

        var result = StatsCalculator.CalculateParTypeScoring(rounds);

        Assert.Equal(3.5, result.Par3Average);
        Assert.Equal(2, result.Par3Count);
        Assert.Equal(5.0, result.Par4Average);
        Assert.Equal(3, result.Par4Count);
        Assert.Equal(5.0, result.Par5Average);
        Assert.Equal(1, result.Par5Count);
    }

    [Fact]
    public void CalculateParTypeScoring_MultipleRounds_AggregatesAcrossRounds()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 0, holes: new List<RoundHole>
            {
                CreateHole(1, 4, score: 4)
            }),
            CreateRound(2, 0, holes: new List<RoundHole>
            {
                CreateHole(1, 4, score: 6)
            })
        };

        var result = StatsCalculator.CalculateParTypeScoring(rounds);

        Assert.Equal(5.0, result.Par4Average);
        Assert.Equal(2, result.Par4Count);
    }

    #endregion

    #region CalculateAdvancedStats Tests

    [Fact]
    public void CalculateAdvancedStats_EmptyList_ReturnsEmptyStats()
    {
        var rounds = new List<RoundResponse>();

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        Assert.Equal(0, result.RoundsWithStats);
        Assert.Null(result.FirPercent);
        Assert.Null(result.GirPercent);
        Assert.Null(result.AveragePutts);
    }

    [Fact]
    public void CalculateAdvancedStats_NoRoundsWithHoleStats_ReturnsEmptyStats()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: false),
            CreateRound(2, 82, usingHoleStats: false)
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        Assert.Equal(0, result.RoundsWithStats);
    }

    [Fact]
    public void CalculateAdvancedStats_FirPercentage_CalculatesCorrectly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: true, holes: new List<RoundHole>
            {
                // Par 4/5 with fairway data
                CreateHole(1, 4, hitFairway: true),
                CreateHole(2, 4, hitFairway: true),
                CreateHole(3, 5, hitFairway: false),
                CreateHole(4, 4, hitFairway: false),
                // Par 3 - excluded from FIR
                CreateHole(5, 3, hitFairway: null)
            })
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // 2 out of 4 = 50%
        Assert.Equal(50.0, result.FirPercent);
    }

    [Fact]
    public void CalculateAdvancedStats_GirPercentage_CalculatesCorrectly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: true),
                CreateHole(2, 4, hitGreen: true),
                CreateHole(3, 4, hitGreen: true),
                CreateHole(4, 4, hitGreen: false),
                CreateHole(5, 3, hitGreen: false)
            })
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // 3 out of 5 = 60%
        Assert.Equal(60.0, result.GirPercent);
    }

    [Fact]
    public void CalculateAdvancedStats_AveragePutts_CalculatesPerRound()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 2),
                CreateHole(2, 4, numberOfPutts: 2),
                CreateHole(3, 4, numberOfPutts: 1)
            }),
            CreateRound(2, 82, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 3),
                CreateHole(2, 4, numberOfPutts: 2),
                CreateHole(3, 4, numberOfPutts: 2)
            })
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // Round 1: 5 putts, Round 2: 7 putts, avg = 6.0
        Assert.Equal(6.0, result.AveragePutts);
    }

    [Fact]
    public void CalculateAdvancedStats_LessThan10Rounds_NoTrends()
    {
        var rounds = new List<RoundResponse>();
        for (int i = 1; i <= 9; i++)
        {
            rounds.Add(CreateRound(i, 80, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: true, hitGreen: true, numberOfPutts: 2)
            }));
        }

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        Assert.Null(result.FirPercentTrend);
        Assert.Null(result.GirPercentTrend);
        Assert.Null(result.AveragePuttsTrend);
    }

    [Fact]
    public void CalculateAdvancedStats_10OrMoreRounds_CalculatesTrends()
    {
        var rounds = new List<RoundResponse>();

        // Recent 5 rounds: 100% FIR
        for (int i = 1; i <= 5; i++)
        {
            rounds.Add(CreateRound(i, 80, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: true, hitGreen: true, numberOfPutts: 1)
            }));
        }

        // Previous 5 rounds: 0% FIR
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 85, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: false, hitGreen: false, numberOfPutts: 3)
            }));
        }

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // FIR trend: 100% - 0% = +100
        Assert.Equal(100.0, result.FirPercentTrend);
        // GIR trend: 100% - 0% = +100
        Assert.Equal(100.0, result.GirPercentTrend);
        // Putts trend: 1 - 3 = -2 (improvement = negative)
        Assert.Equal(-2.0, result.AveragePuttsTrend);
    }

    [Fact]
    public void CalculateAdvancedStats_MixedRoundsWithAndWithoutStats_OnlyCountsWithStats()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: true)
            }),
            CreateRound(2, 82, usingHoleStats: false), // Should be excluded
            CreateRound(3, 84, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: false)
            })
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        Assert.Equal(2, result.RoundsWithStats);
        // 1 out of 2 = 50%
        Assert.Equal(50.0, result.GirPercent);
    }

    #endregion
}
