using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameObToPenaltyOnHoleStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "tee_shot_ob",
                table: "hole_stats",
                newName: "tee_shot_penalty");

            migrationBuilder.RenameColumn(
                name: "approach_shot_ob",
                table: "hole_stats",
                newName: "approach_shot_penalty");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "tee_shot_penalty",
                table: "hole_stats",
                newName: "tee_shot_ob");

            migrationBuilder.RenameColumn(
                name: "approach_shot_penalty",
                table: "hole_stats",
                newName: "approach_shot_ob");
        }
    }
}
