using FairwayFinder.Features.Helpers;

namespace FairwayFinder.Features.Tests.Helpers;

public class GameHandicapHelperTests
{
    #region Test Helpers

    /// <summary>A full eighteen where stroke index happens to equal hole number.</summary>
    private static IEnumerable<(int HoleNumber, int StrokeIndex)> Eighteen()
        => Enumerable.Range(1, 18).Select(n => (n, n));

    /// <summary>
    /// A back nine as it really sits on a card: holes 10-18 carrying the even stroke indexes.
    /// Ranking these 1-9 is the whole reason allocation works on a nine-hole game.
    /// </summary>
    private static IEnumerable<(int HoleNumber, int StrokeIndex)> BackNine()
        => Enumerable.Range(10, 9).Select(n => (n, (n - 9) * 2));

    private static (long, int) Player(long id, int courseHandicap) => (id, courseHandicap);

    #endregion

    #region StrokesReceived

    [Fact]
    public void StrokesReceived_gives_no_strokes_at_scratch()
    {
        foreach (var rank in Enumerable.Range(1, 18))
        {
            Assert.Equal(0, GameHandicapHelper.StrokesReceived(0, rank, 18));
        }
    }

    [Fact]
    public void StrokesReceived_gives_one_stroke_per_hole_at_eighteen()
    {
        foreach (var rank in Enumerable.Range(1, 18))
        {
            Assert.Equal(1, GameHandicapHelper.StrokesReceived(18, rank, 18));
        }
    }

    [Fact]
    public void StrokesReceived_gives_the_extra_strokes_to_the_hardest_holes_at_nine()
    {
        // 9 over 18 holes: ranks 1-9 get a shot, 10-18 get nothing.
        for (var rank = 1; rank <= 9; rank++)
        {
            Assert.Equal(1, GameHandicapHelper.StrokesReceived(9, rank, 18));
        }

        for (var rank = 10; rank <= 18; rank++)
        {
            Assert.Equal(0, GameHandicapHelper.StrokesReceived(9, rank, 18));
        }
    }

    [Fact]
    public void StrokesReceived_gives_two_on_the_hardest_holes_at_twenty_seven()
    {
        // 27 over 18: one everywhere, plus a second on ranks 1-9.
        for (var rank = 1; rank <= 9; rank++)
        {
            Assert.Equal(2, GameHandicapHelper.StrokesReceived(27, rank, 18));
        }

        for (var rank = 10; rank <= 18; rank++)
        {
            Assert.Equal(1, GameHandicapHelper.StrokesReceived(27, rank, 18));
        }
    }

    [Fact]
    public void StrokesReceived_takes_strokes_off_the_easiest_holes_for_a_plus_player()
    {
        // A plus-2 gives two shots back, and they come off the two easiest holes — ranks 17 and 18.
        Assert.Equal(0, GameHandicapHelper.StrokesReceived(-2, 1, 18));
        Assert.Equal(0, GameHandicapHelper.StrokesReceived(-2, 16, 18));
        Assert.Equal(-1, GameHandicapHelper.StrokesReceived(-2, 17, 18));
        Assert.Equal(-1, GameHandicapHelper.StrokesReceived(-2, 18, 18));
    }

    [Fact]
    public void StrokesReceived_allocates_over_a_nine_hole_game()
    {
        // 5 over 9 holes: the five hardest get a shot.
        for (var rank = 1; rank <= 5; rank++)
        {
            Assert.Equal(1, GameHandicapHelper.StrokesReceived(5, rank, 9));
        }

        for (var rank = 6; rank <= 9; rank++)
        {
            Assert.Equal(0, GameHandicapHelper.StrokesReceived(5, rank, 9));
        }
    }

    [Fact]
    public void StrokesReceived_returns_zero_when_the_game_covers_no_holes()
    {
        Assert.Equal(0, GameHandicapHelper.StrokesReceived(12, 1, 0));
    }

    #endregion

    #region PlayingHandicaps

    [Fact]
    public void PlayingHandicaps_keeps_raw_handicaps_when_not_strokes_off_low()
    {
        var result = GameHandicapHelper.PlayingHandicaps(
            [Player(1, 12), Player(2, 4)], allowancePercent: 100, strokesOffLow: false);

        Assert.Equal(12, result[1]);
        Assert.Equal(4, result[2]);
    }

    [Fact]
    public void PlayingHandicaps_puts_the_low_player_off_scratch_when_strokes_off_low()
    {
        var result = GameHandicapHelper.PlayingHandicaps(
            [Player(1, 12), Player(2, 4)], allowancePercent: 100, strokesOffLow: true);

        Assert.Equal(8, result[1]);
        Assert.Equal(0, result[2]);
    }

    [Fact]
    public void PlayingHandicaps_rounds_the_allowance_half_away_from_zero()
    {
        // 90% of 25 is 22.5. Banker's rounding would give 22; golf rounds up.
        var result = GameHandicapHelper.PlayingHandicaps(
            [Player(1, 25)], allowancePercent: 90, strokesOffLow: false);

        Assert.Equal(23, result[1]);
    }

    [Fact]
    public void PlayingHandicaps_applies_the_allowance_before_subtracting_the_low()
    {
        // 90%: 20 -> 18, 10 -> 9. Off low that is 9 and 0. Subtracting first would give 9 -> 8.
        var result = GameHandicapHelper.PlayingHandicaps(
            [Player(1, 20), Player(2, 10)], allowancePercent: 90, strokesOffLow: true);

        Assert.Equal(9, result[1]);
        Assert.Equal(0, result[2]);
    }

    [Fact]
    public void PlayingHandicaps_handles_an_empty_field()
    {
        var result = GameHandicapHelper.PlayingHandicaps([], allowancePercent: 100, strokesOffLow: true);

        Assert.Empty(result);
    }

    #endregion

    #region RankHolesByStrokeIndex

    [Fact]
    public void RankHolesByStrokeIndex_ranks_a_full_card_by_its_index()
    {
        var ranks = GameHandicapHelper.RankHolesByStrokeIndex(Eighteen());

        Assert.Equal(1, ranks[1]);
        Assert.Equal(18, ranks[18]);
    }

    [Fact]
    public void RankHolesByStrokeIndex_ranks_a_back_nine_one_through_nine()
    {
        // Raw indexes here are 2, 4, 6 ... 18. Allocation needs 1..9, not the raw numbers.
        var ranks = GameHandicapHelper.RankHolesByStrokeIndex(BackNine());

        Assert.Equal(9, ranks.Count);
        Assert.Equal(1, ranks[10]);
        Assert.Equal(5, ranks[14]);
        Assert.Equal(9, ranks[18]);
    }

    [Fact]
    public void RankHolesByStrokeIndex_falls_back_to_hole_order_when_no_index_is_set()
    {
        var ranks = GameHandicapHelper.RankHolesByStrokeIndex(
            Enumerable.Range(1, 9).Select(n => (n, 0)));

        foreach (var n in Enumerable.Range(1, 9))
        {
            Assert.Equal(n, ranks[n]);
        }
    }

    [Fact]
    public void RankHolesByStrokeIndex_sorts_unset_indexes_after_the_holes_that_have_one()
    {
        var ranks = GameHandicapHelper.RankHolesByStrokeIndex(
            [(1, 0), (2, 3), (3, 0), (4, 1)]);

        Assert.Equal(1, ranks[4]);
        Assert.Equal(2, ranks[2]);
        Assert.Equal(3, ranks[1]);
        Assert.Equal(4, ranks[3]);
    }

    [Fact]
    public void RankHolesByStrokeIndex_breaks_ties_on_hole_number()
    {
        // Imported courses really do repeat a stroke index. Allocation must still be deterministic.
        var ranks = GameHandicapHelper.RankHolesByStrokeIndex(
            [(7, 2), (3, 2), (5, 1)]);

        Assert.Equal(1, ranks[5]);
        Assert.Equal(2, ranks[3]);
        Assert.Equal(3, ranks[7]);
    }

    #endregion
}
