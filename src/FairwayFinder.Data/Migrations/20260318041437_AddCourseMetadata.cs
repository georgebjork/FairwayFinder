using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "course",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "club_name",
                table: "course",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "course",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "course",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "course",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "course",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "city",
                table: "course");

            migrationBuilder.DropColumn(
                name: "club_name",
                table: "course");

            migrationBuilder.DropColumn(
                name: "country",
                table: "course");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "course");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "course");

            migrationBuilder.DropColumn(
                name: "state",
                table: "course");
        }
    }
}
