using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsCompleteToRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_complete",
                table: "round",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Every round that exists predates incremental posting: it was written whole by the
            // atomic submit path, so it is complete by definition.
            migrationBuilder.Sql("UPDATE round SET is_complete = TRUE;");

            // ix_score_round_id_hole_id below is unique, so it cannot be created while two live
            // score rows share a (round_id, hole_id). Duplicates are corruption rather than data
            // -- UpdateRoundAsync reconciles on score_id, so a client that sent score_id = 0 for
            // a hole it had already saved inserted a second row instead of updating the first.
            // Keep the highest score_id (the most recent write) and soft-delete the rest, along
            // with the hole_stats and shots hanging off them. No-op when there are no duplicates.
            migrationBuilder.Sql("""
                UPDATE hole_stats SET is_deleted = true, updated_on = CURRENT_DATE
                WHERE is_deleted = false AND score_id IN (
                    SELECT s.score_id FROM score s
                    WHERE s.is_deleted = false
                      AND EXISTS (SELECT 1 FROM score s2
                                  WHERE s2.round_id = s.round_id AND s2.hole_id = s.hole_id
                                    AND s2.is_deleted = false AND s2.score_id > s.score_id));

                UPDATE shot SET is_deleted = true, updated_on = CURRENT_DATE
                WHERE is_deleted = false AND score_id IN (
                    SELECT s.score_id FROM score s
                    WHERE s.is_deleted = false
                      AND EXISTS (SELECT 1 FROM score s2
                                  WHERE s2.round_id = s.round_id AND s2.hole_id = s.hole_id
                                    AND s2.is_deleted = false AND s2.score_id > s.score_id));

                -- Last, because the two statements above key off is_deleted = false on score.
                UPDATE score SET is_deleted = true, updated_on = CURRENT_DATE
                WHERE is_deleted = false
                  AND EXISTS (SELECT 1 FROM score s2
                              WHERE s2.round_id = score.round_id AND s2.hole_id = score.hole_id
                                AND s2.is_deleted = false AND s2.score_id > score.score_id);
                """);

            // Superseded by ix_score_round_id_hole_id: round_id is its leading column, and every
            // score query in the codebase also filters is_deleted = false, so the partial index
            // covers them all.
            migrationBuilder.DropIndex(
                name: "IX_score_round_id",
                table: "score");

            migrationBuilder.CreateIndex(
                name: "ix_score_round_id_hole_id",
                table: "score",
                columns: new[] { "round_id", "hole_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_round_user_id_active",
                table: "round",
                column: "user_id",
                unique: true,
                filter: "is_complete = false AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_score_round_id_hole_id",
                table: "score");

            migrationBuilder.DropIndex(
                name: "ix_round_user_id_active",
                table: "round");

            migrationBuilder.DropColumn(
                name: "is_complete",
                table: "round");

            migrationBuilder.CreateIndex(
                name: "IX_score_round_id",
                table: "score",
                column: "round_id");
        }
    }
}
