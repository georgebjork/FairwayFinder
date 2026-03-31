using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShotTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "using_shot_tracking",
                table: "round",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "shot",
                columns: table => new
                {
                    shot_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    score_id = table.Column<long>(type: "bigint", nullable: false),
                    shot_number = table.Column<int>(type: "integer", nullable: false),
                    start_distance = table.Column<int>(type: "integer", nullable: false),
                    start_distance_unit = table.Column<string>(type: "text", nullable: false),
                    start_lie = table.Column<int>(type: "integer", nullable: false),
                    end_distance = table.Column<int>(type: "integer", nullable: true),
                    end_distance_unit = table.Column<string>(type: "text", nullable: true),
                    end_lie = table.Column<int>(type: "integer", nullable: true),
                    penalty_strokes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("shot_pkey", x => x.shot_id);
                    table.ForeignKey(
                        name: "FK_shot_score_score_id",
                        column: x => x.score_id,
                        principalTable: "score",
                        principalColumn: "score_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_shot_score_id",
                table: "shot",
                column: "score_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shot");

            migrationBuilder.DropColumn(
                name: "using_shot_tracking",
                table: "round");
        }
    }
}
