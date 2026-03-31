using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStrokesGainedToRoundStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "sg_approach",
                table: "round_stats",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "sg_around_the_green",
                table: "round_stats",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "sg_off_the_tee",
                table: "round_stats",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "sg_putting",
                table: "round_stats",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "sg_tee_to_green",
                table: "round_stats",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "sg_total",
                table: "round_stats",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sg_approach",
                table: "round_stats");

            migrationBuilder.DropColumn(
                name: "sg_around_the_green",
                table: "round_stats");

            migrationBuilder.DropColumn(
                name: "sg_off_the_tee",
                table: "round_stats");

            migrationBuilder.DropColumn(
                name: "sg_putting",
                table: "round_stats");

            migrationBuilder.DropColumn(
                name: "sg_tee_to_green",
                table: "round_stats");

            migrationBuilder.DropColumn(
                name: "sg_total",
                table: "round_stats");
        }
    }
}
