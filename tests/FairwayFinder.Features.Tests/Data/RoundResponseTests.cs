using FairwayFinder.Features.Data;

namespace FairwayFinder.Features.Tests.Data;

public class RoundResponseTests
{
    private static RoundHole Hole(int holeNumber, int par, short? score = null, bool? hitGreen = null, short? numberOfPutts = null)
    {
        RoundHoleStat? stats = null;
        if (hitGreen.HasValue || numberOfPutts.HasValue)
        {
            stats = new RoundHoleStat { HitGreen = hitGreen, NumberOfPutts = numberOfPutts };
        }

        return new RoundHole
        {
            HoleNumber = holeNumber,
            Par = par,
            Score = score,
            Stats = stats
        };
    }

    #region RoundHole up-and-down classification

    [Fact]
    public void IsUpAndDown_NoStats_ReturnsFalse()
    {
        var hole = Hole(1, 4, score: 4);

        Assert.False(hole.IsUpAndDown);
        Assert.False(hole.IsUpAndDownAttempt);
    }

    [Fact]
    public void IsUpAndDown_MissedGreenOnePuttPar_ReturnsTrue()
    {
        var hole = Hole(1, 4, score: 4, hitGreen: false, numberOfPutts: 1);

        Assert.True(hole.IsUpAndDown);
        Assert.True(hole.IsUpAndDownAttempt);
    }

    [Fact]
    public void IsUpAndDown_MissedGreenOnePuttBirdie_ReturnsTrue()
    {
        // Par 5, missed green, chipped close, 1 putt for birdie
        var hole = Hole(1, 5, score: 4, hitGreen: false, numberOfPutts: 1);

        Assert.True(hole.IsUpAndDown);
        Assert.True(hole.IsUpAndDownAttempt);
    }

    [Fact]
    public void IsUpAndDown_MissedGreenTwoPuttsBogey_AttemptButNotConverted()
    {
        var hole = Hole(1, 4, score: 5, hitGreen: false, numberOfPutts: 2);

        Assert.False(hole.IsUpAndDown);
        Assert.True(hole.IsUpAndDownAttempt);
    }

    [Fact]
    public void IsUpAndDown_MissedGreenOnePuttOverPar_AttemptButNotConverted()
    {
        // 1 putt but still a double bogey - not par or better
        var hole = Hole(1, 4, score: 6, hitGreen: false, numberOfPutts: 1);

        Assert.False(hole.IsUpAndDown);
        Assert.True(hole.IsUpAndDownAttempt);
    }

    [Fact]
    public void IsUpAndDown_HitGreen_NotAnAttempt()
    {
        var hole = Hole(1, 4, score: 4, hitGreen: true, numberOfPutts: 2);

        Assert.False(hole.IsUpAndDown);
        Assert.False(hole.IsUpAndDownAttempt);
    }

    [Fact]
    public void IsUpAndDownAttempt_MissedGreenNoPuttData_ReturnsFalse()
    {
        var hole = Hole(1, 4, score: 5, hitGreen: false);

        Assert.False(hole.IsUpAndDownAttempt);
        Assert.False(hole.IsUpAndDown);
    }

    #endregion

    #region RoundResponse up-and-down aggregates

    [Fact]
    public void UpAndDown_NoAttempts_PercentageIsNull()
    {
        var round = new RoundResponse
        {
            Holes = new List<RoundHole>
            {
                Hole(1, 4, score: 4, hitGreen: true, numberOfPutts: 2)
            }
        };

        Assert.Equal(0, round.UpAndDowns);
        Assert.Equal(0, round.UpAndDownAttempts);
        Assert.Null(round.UpAndDownPercentage);
    }

    [Fact]
    public void UpAndDown_MixedRound_CountsAttemptsAndConversions()
    {
        var round = new RoundResponse
        {
            Holes = new List<RoundHole>
            {
                Hole(1, 4, score: 4, hitGreen: false, numberOfPutts: 1), // up-and-down
                Hole(2, 4, score: 4, hitGreen: false, numberOfPutts: 1), // up-and-down
                Hole(3, 4, score: 5, hitGreen: false, numberOfPutts: 2), // attempt, missed
                Hole(4, 4, score: 6, hitGreen: false, numberOfPutts: 2), // attempt, missed
                Hole(5, 4, score: 5, hitGreen: false, numberOfPutts: 1), // attempt, missed (bogey)
                Hole(6, 4, score: 4, hitGreen: true, numberOfPutts: 2)   // hit green - not an attempt
            }
        };

        Assert.Equal(2, round.UpAndDowns);
        Assert.Equal(5, round.UpAndDownAttempts);
        Assert.Equal(40.0, round.UpAndDownPercentage);
    }

    [Fact]
    public void UpAndDownPercentage_RoundsToOneDecimal()
    {
        var round = new RoundResponse
        {
            Holes = new List<RoundHole>
            {
                Hole(1, 4, score: 4, hitGreen: false, numberOfPutts: 1), // up-and-down
                Hole(2, 4, score: 5, hitGreen: false, numberOfPutts: 2), // attempt
                Hole(3, 4, score: 5, hitGreen: false, numberOfPutts: 2)  // attempt
            }
        };

        // 1 of 3 = 33.333... -> 33.3
        Assert.Equal(33.3, round.UpAndDownPercentage);
    }

    #endregion
}
