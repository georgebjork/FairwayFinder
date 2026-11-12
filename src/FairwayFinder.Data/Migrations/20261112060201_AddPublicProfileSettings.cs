using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicProfileSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "public_slug",
                table: "user_profile",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_course_names",
                table: "user_profile",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_recent_rounds",
                table: "user_profile",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "show_strokes_gained",
                table: "user_profile",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_profile_public_slug",
                table: "user_profile",
                column: "public_slug",
                unique: true,
                filter: "public_slug IS NOT NULL AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_profile_public_slug",
                table: "user_profile");

            migrationBuilder.DropColumn(
                name: "public_slug",
                table: "user_profile");

            migrationBuilder.DropColumn(
                name: "show_course_names",
                table: "user_profile");

            migrationBuilder.DropColumn(
                name: "show_recent_rounds",
                table: "user_profile");

            migrationBuilder.DropColumn(
                name: "show_strokes_gained",
                table: "user_profile");
        }
    }
}
