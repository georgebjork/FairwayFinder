using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMappingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "golf_course_api_course_map",
                columns: table => new
                {
                    golf_course_api_course_map_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    api_course_id = table.Column<int>(type: "integer", nullable: false),
                    course_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("golf_course_api_course_map_pkey", x => x.golf_course_api_course_map_id);
                    table.ForeignKey(
                        name: "FK_golf_course_api_course_map_course_course_id",
                        column: x => x.course_id,
                        principalTable: "course",
                        principalColumn: "course_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_golf_course_api_course_map_api_course_id",
                table: "golf_course_api_course_map",
                column: "api_course_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_golf_course_api_course_map_course_id",
                table: "golf_course_api_course_map",
                column: "course_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "golf_course_api_course_map");
        }
    }
}
