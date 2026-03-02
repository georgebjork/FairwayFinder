using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTgtrMappingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tgtr_course_map",
                columns: table => new
                {
                    tgtr_course_map_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tgtr_course_id = table.Column<int>(type: "integer", nullable: false),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tgtr_course_map_pkey", x => x.tgtr_course_map_id);
                });

            migrationBuilder.CreateTable(
                name: "tgtr_player_map",
                columns: table => new
                {
                    tgtr_player_map_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tgtr_player_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tgtr_player_map_pkey", x => x.tgtr_player_map_id);
                });

            migrationBuilder.CreateTable(
                name: "tgtr_round_map",
                columns: table => new
                {
                    tgtr_round_map_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tgtr_round_id = table.Column<int>(type: "integer", nullable: false),
                    round_id = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tgtr_round_map_pkey", x => x.tgtr_round_map_id);
                });

            migrationBuilder.CreateTable(
                name: "tgtr_teebox_map",
                columns: table => new
                {
                    tgtr_teebox_map_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tgtr_teebox_id = table.Column<int>(type: "integer", nullable: false),
                    teebox_id = table.Column<long>(type: "bigint", nullable: false),
                    tgtr_course_id = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tgtr_teebox_map_pkey", x => x.tgtr_teebox_map_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tgtr_course_map");

            migrationBuilder.DropTable(
                name: "tgtr_player_map");

            migrationBuilder.DropTable(
                name: "tgtr_round_map");

            migrationBuilder.DropTable(
                name: "tgtr_teebox_map");
        }
    }
}
