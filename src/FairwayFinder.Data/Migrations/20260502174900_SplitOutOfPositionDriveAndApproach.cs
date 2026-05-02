using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitOutOfPositionDriveAndApproach : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "out_of_position",
                table: "hole_stats",
                newName: "tee_shot_out_of_position");

            migrationBuilder.AddColumn<bool>(
                name: "approach_shot_out_of_position",
                table: "hole_stats",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "approach_shot_out_of_position",
                table: "hole_stats");

            migrationBuilder.RenameColumn(
                name: "tee_shot_out_of_position",
                table: "hole_stats",
                newName: "out_of_position");
        }
    }
}
