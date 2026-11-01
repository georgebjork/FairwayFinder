using FairwayFinder.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <summary>
    /// Data-only fix, no model change. <c>RoundScoringHelper.ApplyScoringDistribution</c> used to
    /// count a hole-in-one only when <c>score - par &lt;= -3</c>, so an ace on a par 3 (-2) was
    /// stored in <c>round_stats.eagles</c>. This moves each round's par-3 aces from <c>eagles</c> to
    /// <c>hole_in_one</c>. Only rows whose <c>eagles</c> still holds them are touched (TGTR rounds
    /// imported with their own stats may not), so it can't drive a counter negative. It also only
    /// touches rows in the old shape, where <c>hole_in_one</c> counts just the par-4+ aces, so a row
    /// the fixed code wrote before this ran is left alone and re-running it is a no-op.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261031120000_BackfillParThreeAces")]
    public partial class BackfillParThreeAces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE round_stats rs
                SET hole_in_one = rs.hole_in_one + aces.n,
                    eagles      = rs.eagles - aces.n
                FROM (
                    SELECT s.round_id,
                           COUNT(*) FILTER (WHERE h.par = 3)::int AS n,
                           COUNT(*)::int AS total
                    FROM score s
                    JOIN hole h ON h.hole_id = s.hole_id
                    WHERE s.is_deleted = false
                      AND s.hole_score = 1
                    GROUP BY s.round_id
                ) aces
                WHERE rs.round_id = aces.round_id
                  AND aces.n > 0
                  AND rs.is_deleted = false
                  AND rs.eagles >= aces.n
                  AND rs.hole_in_one = aces.total - aces.n;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: after Up, a par-3 ace is indistinguishable from one counted correctly
            // by the fixed helper, so there is nothing to move back.
        }
    }
}
