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
        bool fullRound = true,
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
            FullRound = fullRound,
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

    [Fact]
    public void CalculateAverageScore_SingleRound_ReturnsThatScore_NineHole()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 85, fullRound: false)
        };

        var result = StatsCalculator.CalculateAverageScore(rounds, false);

        Assert.Equal(85.0, result);
    }

    [Fact]
    public void CalculateAverageScore_MultipleRounds_ReturnsCorrectAverage_NineHole()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, fullRound: false),
            CreateRound(2, 85, fullRound: false),
            CreateRound(3, 90, fullRound: false)
        };

        var result = StatsCalculator.CalculateAverageScore(rounds, false);

        Assert.Equal(85.0, result);
    }

    #endregion

    #region CalculateScoreTrend Tests

    [Fact]
    public void CalculateScoreTrend_LessThan10Rounds_ReturnsNull()
    {
        var rounds = new List<RoundResponse>();
        for (int i = 1; i <= 9; i++)
        {
            rounds.Add(CreateRound(i, 80, fullRound: true));
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
            rounds.Add(CreateRound(i, 80, fullRound: true));
        }
        // Previous 5 rounds (indices 5-9) avg 85
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 85, fullRound: true));
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
            rounds.Add(CreateRound(i, 75, fullRound: true));
        }
        // Previous rounds scoring worse (higher)
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 85, fullRound: true));
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
            rounds.Add(CreateRound(i, 90, fullRound: true));
        }
        // Previous rounds scoring better (lower)
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 80, fullRound: true));
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
            rounds.Add(CreateRound(i, 80, fullRound: true));
        }
        // 3 previous rounds avg 85
        for (int i = 4; i <= 6; i++)
        {
            rounds.Add(CreateRound(i, 85, fullRound: true));
        }

        var result = StatsCalculator.CalculateScoreTrend(rounds, windowSize: 3);

        Assert.Equal(-5.0, result);
    }

    [Fact]
    public void CalculateScoreTrend_DefaultsToFullRound()
    {
        var rounds = new List<RoundResponse>();
        // 5 recent full rounds avg 80
        for (int i = 1; i <= 5; i++)
        {
            rounds.Add(CreateRound(i, 80, fullRound: true));
        }
        // 5 previous full rounds avg 85
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 85, fullRound: true));
        }
        // Add some 9-hole rounds that should be ignored by default
        for (int i = 11; i <= 15; i++)
        {
            rounds.Add(CreateRound(i, 40, fullRound: false));
        }

        var result = StatsCalculator.CalculateScoreTrend(rounds);

        // Should use full rounds only: 80 - 85 = -5
        Assert.Equal(-5.0, result);
    }

    [Fact]
    public void CalculateScoreTrend_NineHoleRounds_FiltersCorrectly()
    {
        var rounds = new List<RoundResponse>();
        // 5 recent 9-hole rounds avg 38
        for (int i = 1; i <= 5; i++)
        {
            rounds.Add(CreateRound(i, 38, fullRound: false));
        }
        // 5 previous 9-hole rounds avg 42
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 42, fullRound: false));
        }
        // Add some full rounds that should be ignored
        for (int i = 11; i <= 15; i++)
        {
            rounds.Add(CreateRound(i, 80, fullRound: true));
        }

        var result = StatsCalculator.CalculateScoreTrend(rounds, fullRound: false);

        // Should use 9-hole rounds only: 38 - 42 = -4
        Assert.Equal(-4.0, result);
    }

    [Fact]
    public void CalculateScoreTrend_MixedRounds_FiltersToFullRoundsOnly()
    {
        var rounds = new List<RoundResponse>();
        // Mix of full and 9-hole rounds
        rounds.Add(CreateRound(1, 78, fullRound: true));
        rounds.Add(CreateRound(2, 35, fullRound: false)); // Should be ignored
        rounds.Add(CreateRound(3, 80, fullRound: true));
        rounds.Add(CreateRound(4, 82, fullRound: true));
        rounds.Add(CreateRound(5, 38, fullRound: false)); // Should be ignored
        rounds.Add(CreateRound(6, 79, fullRound: true));
        rounds.Add(CreateRound(7, 81, fullRound: true));
        // Previous 5 full rounds
        rounds.Add(CreateRound(8, 85, fullRound: true));
        rounds.Add(CreateRound(9, 40, fullRound: false)); // Should be ignored
        rounds.Add(CreateRound(10, 86, fullRound: true));
        rounds.Add(CreateRound(11, 84, fullRound: true));
        rounds.Add(CreateRound(12, 87, fullRound: true));
        rounds.Add(CreateRound(13, 88, fullRound: true));

        var result = StatsCalculator.CalculateScoreTrend(rounds, fullRound: true);

        // Full rounds: [78, 80, 82, 79, 81] avg = 80, [85, 86, 84, 87, 88] avg = 86
        // 80 - 86 = -6
        Assert.Equal(-6.0, result);
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
    public void FindBestRound_SingleFullRound_ReturnsThatRound()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 82, "Pine Valley", 10, new DateOnly(2024, 6, 15), fullRound: true)
        };

        var result = StatsCalculator.FindBestRound(rounds, fullRound: true);

        Assert.NotNull(result);
        Assert.Equal(1, result.RoundId);
        Assert.Equal(82, result.Score);
        Assert.Equal("Pine Valley", result.CourseName);
        Assert.Equal(new DateOnly(2024, 6, 15), result.DatePlayed);
    }

    [Fact]
    public void FindBestRound_MultipleFullRounds_ReturnsLowestScore()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 90, "Course A", fullRound: true),
            CreateRound(2, 75, "Course B", fullRound: true),
            CreateRound(3, 85, "Course C", fullRound: true)
        };

        var result = StatsCalculator.FindBestRound(rounds, fullRound: true);

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
            CreateRound(1, 80, "First Course", fullRound: true),
            CreateRound(2, 80, "Second Course", fullRound: true),
            CreateRound(3, 85, "Third Course", fullRound: true)
        };

        var result = StatsCalculator.FindBestRound(rounds, fullRound: true);

        Assert.NotNull(result);
        Assert.Equal(80, result.Score);
        // OrderBy is stable, so first 80 should be returned
    }

    [Fact]
    public void FindBestRound_DefaultsToFullRound()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 40, "Nine Hole Course", fullRound: false), // 9-hole, lower score
            CreateRound(2, 80, "Full Course", fullRound: true)        // 18-hole
        };

        // Default parameter is fullRound: true
        var result = StatsCalculator.FindBestRound(rounds);

        Assert.NotNull(result);
        Assert.Equal(2, result.RoundId);
        Assert.Equal(80, result.Score);
    }

    [Fact]
    public void FindBestRound_NineHoleRound_ReturnsLowestNineHoleScore()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 38, "Course A", fullRound: false),
            CreateRound(2, 42, "Course B", fullRound: false),
            CreateRound(3, 40, "Course C", fullRound: false)
        };

        var result = StatsCalculator.FindBestRound(rounds, fullRound: false);

        Assert.NotNull(result);
        Assert.Equal(1, result.RoundId);
        Assert.Equal(38, result.Score);
        Assert.Equal("Course A", result.CourseName);
    }

    [Fact]
    public void FindBestRound_MixedRounds_FiltersToFullRoundsOnly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 35, "Nine Hole A", fullRound: false),  // 9-hole, lowest overall
            CreateRound(2, 82, "Full Course A", fullRound: true),
            CreateRound(3, 40, "Nine Hole B", fullRound: false),
            CreateRound(4, 78, "Full Course B", fullRound: true)  // Best 18-hole
        };

        var result = StatsCalculator.FindBestRound(rounds, fullRound: true);

        Assert.NotNull(result);
        Assert.Equal(4, result.RoundId);
        Assert.Equal(78, result.Score);
        Assert.Equal("Full Course B", result.CourseName);
    }

    [Fact]
    public void FindBestRound_MixedRounds_FiltersToNineHoleOnly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 35, "Nine Hole A", fullRound: false),  // Best 9-hole
            CreateRound(2, 72, "Full Course A", fullRound: true), // Lower than 9-hole but different category
            CreateRound(3, 40, "Nine Hole B", fullRound: false),
            CreateRound(4, 78, "Full Course B", fullRound: true)
        };

        var result = StatsCalculator.FindBestRound(rounds, fullRound: false);

        Assert.NotNull(result);
        Assert.Equal(1, result.RoundId);
        Assert.Equal(35, result.Score);
        Assert.Equal("Nine Hole A", result.CourseName);
    }

    [Fact]
    public void FindBestRound_NoMatchingRoundType_ReturnsNull()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 40, "Nine Hole Course", fullRound: false),
            CreateRound(2, 42, "Nine Hole Course", fullRound: false)
        };

        // Searching for full rounds when none exist should return null
        var result = StatsCalculator.FindBestRound(rounds, fullRound: true);
        
        Assert.Null(result);
    }

    [Fact]
    public void FindBestRound_OnlyNineHoleRounds_SearchingForNineHole_ReturnsResult()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 38, "Nine Hole A", fullRound: false),
            CreateRound(2, 42, "Nine Hole B", fullRound: false)
        };

        var result = StatsCalculator.FindBestRound(rounds, fullRound: false);

        Assert.NotNull(result);
        Assert.Equal(1, result.RoundId);
        Assert.Equal(38, result.Score);
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
            CreateRound(1, 80, datePlayed: new DateOnly(2024, 6, 3), fullRound: true),
            CreateRound(2, 85, datePlayed: new DateOnly(2024, 6, 2), fullRound: true),
            CreateRound(3, 90, datePlayed: new DateOnly(2024, 6, 1), fullRound: true)
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
            rounds.Add(CreateRound(i, 80 + i, fullRound: true));
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
            CreateRound(42, 78, "Augusta National", datePlayed: new DateOnly(2024, 4, 14), fullRound: true)
        };

        var result = StatsCalculator.BuildScoreTrend(rounds, 10);

        Assert.Single(result);
        Assert.Equal(42, result[0].RoundId);
        Assert.Equal(78, result[0].Score);
        Assert.Equal("Augusta National", result[0].CourseName);
        Assert.Equal(new DateOnly(2024, 4, 14), result[0].DatePlayed);
    }

    [Fact]
    public void BuildScoreTrend_DefaultsToFullRound()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, "Full Course A", datePlayed: new DateOnly(2024, 6, 3), fullRound: true),
            CreateRound(2, 40, "Nine Hole", datePlayed: new DateOnly(2024, 6, 2), fullRound: false), // Should be ignored
            CreateRound(3, 85, "Full Course B", datePlayed: new DateOnly(2024, 6, 1), fullRound: true)
        };

        var result = StatsCalculator.BuildScoreTrend(rounds, 10);

        Assert.Equal(2, result.Count);
        // Should only include full rounds, reversed
        Assert.Equal(3, result[0].RoundId);
        Assert.Equal(1, result[1].RoundId);
    }

    [Fact]
    public void BuildScoreTrend_NineHoleRounds_FiltersCorrectly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 38, "Nine Hole A", datePlayed: new DateOnly(2024, 6, 3), fullRound: false),
            CreateRound(2, 80, "Full Course", datePlayed: new DateOnly(2024, 6, 2), fullRound: true), // Should be ignored
            CreateRound(3, 42, "Nine Hole B", datePlayed: new DateOnly(2024, 6, 1), fullRound: false)
        };

        var result = StatsCalculator.BuildScoreTrend(rounds, 10, fullRound: false);

        Assert.Equal(2, result.Count);
        // Should only include 9-hole rounds, reversed
        Assert.Equal(3, result[0].RoundId);
        Assert.Equal(1, result[1].RoundId);
    }

    [Fact]
    public void BuildScoreTrend_MixedRounds_FiltersToFullRoundsOnly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 78, "Course A", fullRound: true),
            CreateRound(2, 35, "Nine Hole", fullRound: false),
            CreateRound(3, 80, "Course B", fullRound: true),
            CreateRound(4, 38, "Nine Hole", fullRound: false),
            CreateRound(5, 82, "Course C", fullRound: true)
        };

        var result = StatsCalculator.BuildScoreTrend(rounds, 10, fullRound: true);

        Assert.Equal(3, result.Count);
        // Should only include full rounds [1, 3, 5], reversed to [5, 3, 1]
        Assert.Equal(5, result[0].RoundId);
        Assert.Equal(3, result[1].RoundId);
        Assert.Equal(1, result[2].RoundId);
    }

    [Fact]
    public void BuildScoreTrend_MixedRounds_FiltersToNineHoleOnly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 78, "Course A", fullRound: true),
            CreateRound(2, 35, "Nine Hole A", fullRound: false),
            CreateRound(3, 80, "Course B", fullRound: true),
            CreateRound(4, 38, "Nine Hole B", fullRound: false),
            CreateRound(5, 82, "Course C", fullRound: true)
        };

        var result = StatsCalculator.BuildScoreTrend(rounds, 10, fullRound: false);

        Assert.Equal(2, result.Count);
        // Should only include 9-hole rounds [2, 4], reversed to [4, 2]
        Assert.Equal(4, result[0].RoundId);
        Assert.Equal(2, result[1].RoundId);
    }

    [Fact]
    public void BuildScoreTrend_CountLimitsAfterFiltering()
    {
        var rounds = new List<RoundResponse>();
        // Add 10 full rounds
        for (int i = 1; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 80 + i, fullRound: true));
        }
        // Add some 9-hole rounds interspersed
        for (int i = 11; i <= 15; i++)
        {
            rounds.Add(CreateRound(i, 40, fullRound: false));
        }

        var result = StatsCalculator.BuildScoreTrend(rounds, 5, fullRound: true);

        // Should filter to full rounds first, then take 5
        Assert.Equal(5, result.Count);
        // Takes first 5 full rounds [1,2,3,4,5], then reverses
        Assert.Equal(5, result[0].RoundId);
        Assert.Equal(1, result[4].RoundId);
    }

    #endregion

    #region BuildPuttsTrend Tests

    [Fact]
    public void BuildPuttsTrend_EmptyList_ReturnsEmptyList()
    {
        var rounds = new List<RoundResponse>();

        var result = StatsCalculator.BuildPuttsTrend(rounds, 10);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildPuttsTrend_NoRoundsWithPuttData_ReturnsEmptyList()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, fullRound: true, usingHoleStats: false),
            CreateRound(2, 82, fullRound: true, usingHoleStats: false)
        };

        var result = StatsCalculator.BuildPuttsTrend(rounds, 10);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildPuttsTrend_RoundsWithPutts_ReturnsCorrectData()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, "Course A", fullRound: true, usingHoleStats: true, 
                datePlayed: new DateOnly(2024, 6, 3),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, numberOfPutts: 2),
                    CreateHole(2, 4, numberOfPutts: 2),
                    CreateHole(3, 4, numberOfPutts: 1)
                }),
            CreateRound(2, 82, "Course B", fullRound: true, usingHoleStats: true, 
                datePlayed: new DateOnly(2024, 6, 2),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, numberOfPutts: 3),
                    CreateHole(2, 4, numberOfPutts: 2),
                    CreateHole(3, 4, numberOfPutts: 2)
                }),
            CreateRound(3, 84, "Course C", fullRound: true, usingHoleStats: true, 
                datePlayed: new DateOnly(2024, 6, 1),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, numberOfPutts: 2),
                    CreateHole(2, 4, numberOfPutts: 2),
                    CreateHole(3, 4, numberOfPutts: 2)
                })
        };

        var result = StatsCalculator.BuildPuttsTrend(rounds, 10);

        Assert.Equal(3, result.Count);
        // Should be reversed (oldest first)
        Assert.Equal(3, result[0].RoundId);
        Assert.Equal(6, result[0].Putts); // 2+2+2
        Assert.Equal("Course C", result[0].CourseName);
        
        Assert.Equal(2, result[1].RoundId);
        Assert.Equal(7, result[1].Putts); // 3+2+2
        
        Assert.Equal(1, result[2].RoundId);
        Assert.Equal(5, result[2].Putts); // 2+2+1
    }

    [Fact]
    public void BuildPuttsTrend_DefaultsToFullRound()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, fullRound: true, usingHoleStats: true, 
                holes: new List<RoundHole> { CreateHole(1, 4, numberOfPutts: 2) }),
            CreateRound(2, 40, fullRound: false, usingHoleStats: true, 
                holes: new List<RoundHole> { CreateHole(1, 4, numberOfPutts: 1) }), // Should be excluded
            CreateRound(3, 82, fullRound: true, usingHoleStats: true, 
                holes: new List<RoundHole> { CreateHole(1, 4, numberOfPutts: 3) })
        };

        var result = StatsCalculator.BuildPuttsTrend(rounds, 10);

        Assert.Equal(2, result.Count);
        // Only full rounds
        Assert.Equal(3, result[0].RoundId);
        Assert.Equal(1, result[1].RoundId);
    }

    [Fact]
    public void BuildPuttsTrend_NineHoleRounds_FiltersCorrectly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, fullRound: true, usingHoleStats: true, 
                holes: new List<RoundHole> { CreateHole(1, 4, numberOfPutts: 2) }), // Should be excluded
            CreateRound(2, 40, fullRound: false, usingHoleStats: true, 
                holes: new List<RoundHole> { CreateHole(1, 4, numberOfPutts: 1) }),
            CreateRound(3, 42, fullRound: false, usingHoleStats: true, 
                holes: new List<RoundHole> { CreateHole(1, 4, numberOfPutts: 2) })
        };

        var result = StatsCalculator.BuildPuttsTrend(rounds, 10, fullRound: false);

        Assert.Equal(2, result.Count);
        // Only 9-hole rounds, reversed
        Assert.Equal(3, result[0].RoundId);
        Assert.Equal(2, result[1].RoundId);
    }

    [Fact]
    public void BuildPuttsTrend_ExcludesRoundsWithZeroPutts()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, fullRound: true, usingHoleStats: true, 
                holes: new List<RoundHole> { CreateHole(1, 4, numberOfPutts: 2) }),
            CreateRound(2, 82, fullRound: true, usingHoleStats: true, 
                holes: new List<RoundHole> { CreateHole(1, 4, numberOfPutts: null) }), // No putt data
            CreateRound(3, 84, fullRound: true, usingHoleStats: true, 
                holes: new List<RoundHole> { CreateHole(1, 4, numberOfPutts: 3) })
        };

        var result = StatsCalculator.BuildPuttsTrend(rounds, 10);

        Assert.Equal(2, result.Count);
        // Round 2 excluded (0 putts from null)
        Assert.Equal(3, result[0].RoundId);
        Assert.Equal(1, result[1].RoundId);
    }

    [Fact]
    public void BuildPuttsTrend_LimitsToCount()
    {
        var rounds = new List<RoundResponse>();
        for (int i = 1; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 80, fullRound: true, usingHoleStats: true,
                holes: new List<RoundHole> { CreateHole(1, 4, numberOfPutts: 2) }));
        }

        var result = StatsCalculator.BuildPuttsTrend(rounds, 5);

        Assert.Equal(5, result.Count);
        // Takes first 5, then reverses
        Assert.Equal(5, result[0].RoundId);
        Assert.Equal(1, result[4].RoundId);
    }

    [Fact]
    public void BuildPuttsTrend_MapsAllProperties()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(42, 78, "Augusta National", fullRound: true, usingHoleStats: true,
                datePlayed: new DateOnly(2024, 4, 14),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, numberOfPutts: 1),
                    CreateHole(2, 4, numberOfPutts: 2)
                })
        };

        var result = StatsCalculator.BuildPuttsTrend(rounds, 10);

        Assert.Single(result);
        Assert.Equal(42, result[0].RoundId);
        Assert.Equal(3, result[0].Putts);
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
        Assert.Equal(85.0, result[0].Average18HoleScore);
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

        Assert.Equal(71.0, easy.Average18HoleScore);
        Assert.Equal(92.5, hard.Average18HoleScore);
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
        Assert.Null(result.Average18HolePutts);
        Assert.Null(result.Average9HolePutts);
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
    public void CalculateAdvancedStats_FirPercentage_CalculatesFromAllRounds()
    {
        var rounds = new List<RoundResponse>
        {
            // 18-hole round with FIR data
            CreateRound(1, 80, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: true),
                CreateHole(2, 4, hitFairway: true),
                CreateHole(3, 5, hitFairway: false),
                CreateHole(4, 4, hitFairway: false),
                CreateHole(5, 3, hitFairway: null) // Par 3 - excluded from FIR
            }),
            // 9-hole round - now included in FIR calculation
            CreateRound(2, 40, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: true),
                CreateHole(2, 4, hitFairway: true)
            })
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // All rounds: 4 out of 6 = 66.7%
        Assert.Equal(66.7, result.FirPercent);
    }

    [Fact]
    public void CalculateAdvancedStats_GirPercentage_CalculatesFromAllRounds()
    {
        var rounds = new List<RoundResponse>
        {
            // 18-hole round
            CreateRound(1, 80, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: true),
                CreateHole(2, 4, hitGreen: true),
                CreateHole(3, 4, hitGreen: true),
                CreateHole(4, 4, hitGreen: false),
                CreateHole(5, 3, hitGreen: false)
            }),
            // 9-hole round - now included in GIR calculation
            CreateRound(2, 40, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: true),
                CreateHole(2, 4, hitGreen: true)
            })
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // All rounds: 5 out of 7 = 71.4%
        Assert.Equal(71.4, result.GirPercent);
    }

    [Fact]
    public void CalculateAdvancedStats_Average18HolePutts_CalculatesCorrectly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 2),
                CreateHole(2, 4, numberOfPutts: 2),
                CreateHole(3, 4, numberOfPutts: 1)
            }),
            CreateRound(2, 82, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 3),
                CreateHole(2, 4, numberOfPutts: 2),
                CreateHole(3, 4, numberOfPutts: 2)
            })
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // Round 1: 5 putts, Round 2: 7 putts, avg = 6.0
        Assert.Equal(6.0, result.Average18HolePutts);
        Assert.Null(result.Average9HolePutts); // No 9-hole rounds
    }

    [Fact]
    public void CalculateAdvancedStats_Average9HolePutts_CalculatesCorrectly()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 40, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 2),
                CreateHole(2, 4, numberOfPutts: 1),
                CreateHole(3, 4, numberOfPutts: 2)
            }),
            CreateRound(2, 42, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 2),
                CreateHole(2, 4, numberOfPutts: 2),
                CreateHole(3, 4, numberOfPutts: 2)
            })
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // Round 1: 5 putts, Round 2: 6 putts, avg = 5.5
        Assert.Equal(5.5, result.Average9HolePutts);
        Assert.Null(result.Average18HolePutts); // No 18-hole rounds
    }

    [Fact]
    public void CalculateAdvancedStats_MixedRoundTypes_CalculatesPuttsSeparately()
    {
        var rounds = new List<RoundResponse>
        {
            // 18-hole rounds
            CreateRound(1, 80, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 2),
                CreateHole(2, 4, numberOfPutts: 2)
            }),
            CreateRound(2, 82, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 3),
                CreateHole(2, 4, numberOfPutts: 3)
            }),
            // 9-hole rounds
            CreateRound(3, 40, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 1),
                CreateHole(2, 4, numberOfPutts: 1)
            }),
            CreateRound(4, 42, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 2),
                CreateHole(2, 4, numberOfPutts: 2)
            })
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // 18-hole: Round 1 = 4, Round 2 = 6, avg = 5.0
        Assert.Equal(5.0, result.Average18HolePutts);
        // 9-hole: Round 3 = 2, Round 4 = 4, avg = 3.0
        Assert.Equal(3.0, result.Average9HolePutts);
    }

    [Fact]
    public void CalculateAdvancedStats_LessThan10Rounds_NoTrends()
    {
        var rounds = new List<RoundResponse>();
        for (int i = 1; i <= 9; i++)
        {
            rounds.Add(CreateRound(i, 80, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: true, hitGreen: true, numberOfPutts: 2)
            }));
        }

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        Assert.Null(result.FirPercentTrend);
        Assert.Null(result.GirPercentTrend);
        Assert.Null(result.Average18HolePuttsTrend);
        Assert.Null(result.Average9HolePuttsTrend);
    }

    [Fact]
    public void CalculateAdvancedStats_10OrMoreRounds_CalculatesFirGirTrends()
    {
        var rounds = new List<RoundResponse>();

        // Recent 5 rounds: 100% FIR/GIR
        for (int i = 1; i <= 5; i++)
        {
            rounds.Add(CreateRound(i, 80, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: true, hitGreen: true, numberOfPutts: 1)
            }));
        }

        // Previous 5 rounds: 0% FIR/GIR
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 85, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: false, hitGreen: false, numberOfPutts: 3)
            }));
        }

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // FIR trend: 100% - 0% = +100
        Assert.Equal(100.0, result.FirPercentTrend);
        // GIR trend: 100% - 0% = +100
        Assert.Equal(100.0, result.GirPercentTrend);
    }

    [Fact]
    public void CalculateAdvancedStats_10OrMore18HoleRounds_Calculates18HolePuttsTrend()
    {
        var rounds = new List<RoundResponse>();

        // Recent 5 18-hole rounds: 1 putt per hole
        for (int i = 1; i <= 5; i++)
        {
            rounds.Add(CreateRound(i, 80, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: true, hitGreen: true, numberOfPutts: 1)
            }));
        }

        // Previous 5 18-hole rounds: 3 putts per hole
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 85, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: false, hitGreen: false, numberOfPutts: 3)
            }));
        }

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // 18-hole putts trend: 1 - 3 = -2 (improvement = negative)
        Assert.Equal(-2.0, result.Average18HolePuttsTrend);
    }

    [Fact]
    public void CalculateAdvancedStats_10OrMore9HoleRounds_Calculates9HolePuttsTrend()
    {
        var rounds = new List<RoundResponse>();

        // Recent 5 9-hole rounds: 1 putt per hole
        for (int i = 1; i <= 5; i++)
        {
            rounds.Add(CreateRound(i, 40, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 1)
            }));
        }

        // Previous 5 9-hole rounds: 3 putts per hole
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 45, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 3)
            }));
        }

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // 9-hole putts trend: 1 - 3 = -2 (improvement = negative)
        Assert.Equal(-2.0, result.Average9HolePuttsTrend);
    }

    [Fact]
    public void CalculateAdvancedStats_MixedRoundTypes_CalculatesTrendsSeparately()
    {
        var rounds = new List<RoundResponse>();

        // Recent 5 18-hole rounds: 1 putt
        for (int i = 1; i <= 5; i++)
        {
            rounds.Add(CreateRound(i, 80, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: true, hitGreen: true, numberOfPutts: 1)
            }));
        }
        // Previous 5 18-hole rounds: 3 putts
        for (int i = 6; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 85, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: false, hitGreen: false, numberOfPutts: 3)
            }));
        }
        // Recent 5 9-hole rounds: 2 putts
        for (int i = 11; i <= 15; i++)
        {
            rounds.Add(CreateRound(i, 40, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 2)
            }));
        }
        // Previous 5 9-hole rounds: 1 putt
        for (int i = 16; i <= 20; i++)
        {
            rounds.Add(CreateRound(i, 38, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, numberOfPutts: 1)
            }));
        }

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // 18-hole putts trend: 1 - 3 = -2 (improvement)
        Assert.Equal(-2.0, result.Average18HolePuttsTrend);
        // 9-hole putts trend: 2 - 1 = +1 (regression)
        Assert.Equal(1.0, result.Average9HolePuttsTrend);
    }

    [Fact]
    public void CalculateAdvancedStats_MixedRoundsWithAndWithoutStats_OnlyCountsWithStats()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: true)
            }),
            CreateRound(2, 82, usingHoleStats: false), // Should be excluded
            CreateRound(3, 84, usingHoleStats: true, fullRound: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: false)
            })
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        Assert.Equal(2, result.RoundsWithStats);
        // 1 out of 2 = 50%
        Assert.Equal(50.0, result.GirPercent);
    }

    [Fact]
    public void CalculateAdvancedStats_Only9HoleRounds_CalculatesFirGirStats()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 40, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: true, hitGreen: true, numberOfPutts: 2)
            }),
            CreateRound(2, 42, usingHoleStats: true, fullRound: false, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: false, hitGreen: false, numberOfPutts: 3)
            })
        };

        var result = StatsCalculator.CalculateAdvancedStats(rounds);

        // FIR/GIR now calculated from all rounds including 9-hole
        Assert.Equal(50.0, result.FirPercent); // 1 out of 2
        Assert.Equal(50.0, result.GirPercent); // 1 out of 2
        // 9-hole putts should be calculated
        Assert.Equal(2.5, result.Average9HolePutts);
    }

    #endregion

    #region BuildFirTrend Tests

    [Fact]
    public void BuildFirTrend_EmptyList_ReturnsEmptyList()
    {
        var rounds = new List<RoundResponse>();

        var result = StatsCalculator.BuildFirTrend(rounds, 10);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildFirTrend_NoRoundsWithHoleStats_ReturnsEmptyList()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: false),
            CreateRound(2, 82, usingHoleStats: false)
        };

        var result = StatsCalculator.BuildFirTrend(rounds, 10);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildFirTrend_NoFairwayData_ReturnsEmptyList()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: null), // No fairway data
                CreateHole(2, 3, hitFairway: null)  // Par 3, excluded anyway
            })
        };

        var result = StatsCalculator.BuildFirTrend(rounds, 10);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildFirTrend_ExcludesPar3Holes()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, "Course A", usingHoleStats: true, 
                datePlayed: new DateOnly(2024, 6, 3),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 3, hitFairway: true),  // Par 3 - excluded
                    CreateHole(2, 4, hitFairway: true),  // Par 4 - included
                    CreateHole(3, 5, hitFairway: false), // Par 5 - included
                    CreateHole(4, 4, hitFairway: true)   // Par 4 - included
                })
        };

        var result = StatsCalculator.BuildFirTrend(rounds, 10);

        Assert.Single(result);
        // 2 out of 3 par 4/5 holes = 66.7%
        Assert.Equal(66.7, result[0].FirPercent);
        Assert.Equal(2, result[0].FairwaysHit);
        Assert.Equal(3, result[0].FairwayAttempts);
    }

    [Fact]
    public void BuildFirTrend_MultipleRounds_ReturnsCorrectDataReversed()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, "Course A", usingHoleStats: true, 
                datePlayed: new DateOnly(2024, 6, 3),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, hitFairway: true),
                    CreateHole(2, 4, hitFairway: true)
                }),
            CreateRound(2, 82, "Course B", usingHoleStats: true, 
                datePlayed: new DateOnly(2024, 6, 2),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, hitFairway: true),
                    CreateHole(2, 4, hitFairway: false)
                }),
            CreateRound(3, 84, "Course C", usingHoleStats: true, 
                datePlayed: new DateOnly(2024, 6, 1),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, hitFairway: false),
                    CreateHole(2, 4, hitFairway: false)
                })
        };

        var result = StatsCalculator.BuildFirTrend(rounds, 10);

        Assert.Equal(3, result.Count);
        // Should be reversed (oldest first)
        Assert.Equal(3, result[0].RoundId);
        Assert.Equal(0.0, result[0].FirPercent); // 0/2
        
        Assert.Equal(2, result[1].RoundId);
        Assert.Equal(50.0, result[1].FirPercent); // 1/2
        
        Assert.Equal(1, result[2].RoundId);
        Assert.Equal(100.0, result[2].FirPercent); // 2/2
    }

    [Fact]
    public void BuildFirTrend_MapsAllProperties()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(42, 78, "Augusta National", usingHoleStats: true,
                datePlayed: new DateOnly(2024, 4, 14),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, hitFairway: true),
                    CreateHole(2, 5, hitFairway: true),
                    CreateHole(3, 4, hitFairway: false)
                })
        };

        var result = StatsCalculator.BuildFirTrend(rounds, 10);

        Assert.Single(result);
        Assert.Equal(42, result[0].RoundId);
        Assert.Equal(66.7, result[0].FirPercent);
        Assert.Equal(2, result[0].FairwaysHit);
        Assert.Equal(3, result[0].FairwayAttempts);
        Assert.Equal("Augusta National", result[0].CourseName);
        Assert.Equal(new DateOnly(2024, 4, 14), result[0].DatePlayed);
    }

    [Fact]
    public void BuildFirTrend_LimitsToCount()
    {
        var rounds = new List<RoundResponse>();
        for (int i = 1; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 80, usingHoleStats: true,
                holes: new List<RoundHole> { CreateHole(1, 4, hitFairway: true) }));
        }

        var result = StatsCalculator.BuildFirTrend(rounds, 5);

        Assert.Equal(5, result.Count);
        // Takes first 5, then reverses
        Assert.Equal(5, result[0].RoundId);
        Assert.Equal(1, result[4].RoundId);
    }

    [Fact]
    public void BuildFirTrend_MixedRoundsWithAndWithoutData_OnlyIncludesRoundsWithData()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: true)
            }),
            CreateRound(2, 82, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: null) // No fairway data
            }),
            CreateRound(3, 84, usingHoleStats: false), // No hole stats
            CreateRound(4, 86, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitFairway: false)
            })
        };

        var result = StatsCalculator.BuildFirTrend(rounds, 10);

        Assert.Equal(2, result.Count);
        // Only rounds 1 and 4 have FIR data
        Assert.Equal(4, result[0].RoundId);
        Assert.Equal(1, result[1].RoundId);
    }

    #endregion

    #region BuildGirTrend Tests

    [Fact]
    public void BuildGirTrend_EmptyList_ReturnsEmptyList()
    {
        var rounds = new List<RoundResponse>();

        var result = StatsCalculator.BuildGirTrend(rounds, 10);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildGirTrend_NoRoundsWithHoleStats_ReturnsEmptyList()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: false),
            CreateRound(2, 82, usingHoleStats: false)
        };

        var result = StatsCalculator.BuildGirTrend(rounds, 10);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildGirTrend_NoGreenData_ReturnsEmptyList()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: null),
                CreateHole(2, 3, hitGreen: null)
            })
        };

        var result = StatsCalculator.BuildGirTrend(rounds, 10);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildGirTrend_IncludesAllParTypes()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, "Course A", usingHoleStats: true, 
                datePlayed: new DateOnly(2024, 6, 3),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 3, hitGreen: true),  // Par 3 - included
                    CreateHole(2, 4, hitGreen: true),  // Par 4 - included
                    CreateHole(3, 5, hitGreen: false), // Par 5 - included
                    CreateHole(4, 4, hitGreen: false)  // Par 4 - included
                })
        };

        var result = StatsCalculator.BuildGirTrend(rounds, 10);

        Assert.Single(result);
        // 2 out of 4 = 50%
        Assert.Equal(50.0, result[0].GirPercent);
        Assert.Equal(2, result[0].GreensHit);
        Assert.Equal(4, result[0].GreenAttempts);
    }

    [Fact]
    public void BuildGirTrend_MultipleRounds_ReturnsCorrectDataReversed()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, "Course A", usingHoleStats: true, 
                datePlayed: new DateOnly(2024, 6, 3),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, hitGreen: true),
                    CreateHole(2, 4, hitGreen: true)
                }),
            CreateRound(2, 82, "Course B", usingHoleStats: true, 
                datePlayed: new DateOnly(2024, 6, 2),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, hitGreen: true),
                    CreateHole(2, 4, hitGreen: false)
                }),
            CreateRound(3, 84, "Course C", usingHoleStats: true, 
                datePlayed: new DateOnly(2024, 6, 1),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, hitGreen: false),
                    CreateHole(2, 4, hitGreen: false)
                })
        };

        var result = StatsCalculator.BuildGirTrend(rounds, 10);

        Assert.Equal(3, result.Count);
        // Should be reversed (oldest first)
        Assert.Equal(3, result[0].RoundId);
        Assert.Equal(0.0, result[0].GirPercent); // 0/2
        
        Assert.Equal(2, result[1].RoundId);
        Assert.Equal(50.0, result[1].GirPercent); // 1/2
        
        Assert.Equal(1, result[2].RoundId);
        Assert.Equal(100.0, result[2].GirPercent); // 2/2
    }

    [Fact]
    public void BuildGirTrend_MapsAllProperties()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(42, 78, "Augusta National", usingHoleStats: true,
                datePlayed: new DateOnly(2024, 4, 14),
                holes: new List<RoundHole>
                {
                    CreateHole(1, 4, hitGreen: true),
                    CreateHole(2, 5, hitGreen: true),
                    CreateHole(3, 3, hitGreen: false)
                })
        };

        var result = StatsCalculator.BuildGirTrend(rounds, 10);

        Assert.Single(result);
        Assert.Equal(42, result[0].RoundId);
        Assert.Equal(66.7, result[0].GirPercent);
        Assert.Equal(2, result[0].GreensHit);
        Assert.Equal(3, result[0].GreenAttempts);
        Assert.Equal("Augusta National", result[0].CourseName);
        Assert.Equal(new DateOnly(2024, 4, 14), result[0].DatePlayed);
    }

    [Fact]
    public void BuildGirTrend_LimitsToCount()
    {
        var rounds = new List<RoundResponse>();
        for (int i = 1; i <= 10; i++)
        {
            rounds.Add(CreateRound(i, 80, usingHoleStats: true,
                holes: new List<RoundHole> { CreateHole(1, 4, hitGreen: true) }));
        }

        var result = StatsCalculator.BuildGirTrend(rounds, 5);

        Assert.Equal(5, result.Count);
        // Takes first 5, then reverses
        Assert.Equal(5, result[0].RoundId);
        Assert.Equal(1, result[4].RoundId);
    }

    [Fact]
    public void BuildGirTrend_MixedRoundsWithAndWithoutData_OnlyIncludesRoundsWithData()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 80, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: true)
            }),
            CreateRound(2, 82, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: null) // No green data
            }),
            CreateRound(3, 84, usingHoleStats: false), // No hole stats
            CreateRound(4, 86, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: false)
            })
        };

        var result = StatsCalculator.BuildGirTrend(rounds, 10);

        Assert.Equal(2, result.Count);
        // Only rounds 1 and 4 have GIR data
        Assert.Equal(4, result[0].RoundId);
        Assert.Equal(1, result[1].RoundId);
    }

    [Fact]
    public void BuildGirTrend_PerfectGir_Returns100Percent()
    {
        var rounds = new List<RoundResponse>
        {
            CreateRound(1, 72, usingHoleStats: true, holes: new List<RoundHole>
            {
                CreateHole(1, 4, hitGreen: true),
                CreateHole(2, 3, hitGreen: true),
                CreateHole(3, 5, hitGreen: true),
                CreateHole(4, 4, hitGreen: true)
            })
        };

        var result = StatsCalculator.BuildGirTrend(rounds, 10);

        Assert.Single(result);
        Assert.Equal(100.0, result[0].GirPercent);
        Assert.Equal(4, result[0].GreensHit);
        Assert.Equal(4, result[0].GreenAttempts);
    }

    #endregion
}
