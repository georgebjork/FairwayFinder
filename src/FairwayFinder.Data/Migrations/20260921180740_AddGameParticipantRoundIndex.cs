using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameParticipantRoundIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_game_participant_round",
                table: "game_participant",
                column: "round_id",
                filter: "round_id IS NOT NULL AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_game_participant_round",
                table: "game_participant");
        }
    }
}
