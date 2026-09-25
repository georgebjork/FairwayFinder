using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game",
                columns: table => new
                {
                    game_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_type = table.Column<int>(type: "integer", nullable: false),
                    course_id = table.Column<long>(type: "bigint", nullable: false),
                    date_played = table.Column<DateOnly>(type: "date", nullable: false),
                    host_user_id = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    join_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    full_round = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    front_nine = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    back_nine = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    use_net = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    handicap_allowance_percent = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    strokes_off_low = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    skins_carryover = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    skins_value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    final_scoreboard = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("game_pkey", x => x.game_id);
                    table.ForeignKey(
                        name: "FK_game_course_course_id",
                        column: x => x.course_id,
                        principalTable: "course",
                        principalColumn: "course_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "game_participant",
                columns: table => new
                {
                    game_participant_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: true),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    round_id = table.Column<long>(type: "bigint", nullable: true),
                    teebox_id = table.Column<long>(type: "bigint", nullable: false),
                    course_handicap = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    team = table.Column<int>(type: "integer", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("game_participant_pkey", x => x.game_participant_id);
                    table.ForeignKey(
                        name: "FK_game_participant_game_game_id",
                        column: x => x.game_id,
                        principalTable: "game",
                        principalColumn: "game_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_game_participant_teebox_teebox_id",
                        column: x => x.teebox_id,
                        principalTable: "teebox",
                        principalColumn: "teebox_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "game_hole_score",
                columns: table => new
                {
                    game_hole_score_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    game_participant_id = table.Column<long>(type: "bigint", nullable: false),
                    hole_number = table.Column<int>(type: "integer", nullable: false),
                    strokes = table.Column<short>(type: "smallint", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("game_hole_score_pkey", x => x.game_hole_score_id);
                    table.ForeignKey(
                        name: "FK_game_hole_score_game_participant_game_participant_id",
                        column: x => x.game_participant_id,
                        principalTable: "game_participant",
                        principalColumn: "game_participant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_course_id",
                table: "game",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_host_state",
                table: "game",
                columns: new[] { "host_user_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_game_join_code_live",
                table: "game",
                column: "join_code",
                unique: true,
                filter: "state < 2 AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_game_hole_score_participant_hole",
                table: "game_hole_score",
                columns: new[] { "game_participant_id", "hole_number" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_game_participant_game_user",
                table: "game_participant",
                columns: new[] { "game_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_game_participant_teebox_id",
                table: "game_participant",
                column: "teebox_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_participant_user",
                table: "game_participant",
                columns: new[] { "user_id", "game_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_hole_score");

            migrationBuilder.DropTable(
                name: "game_participant");

            migrationBuilder.DropTable(
                name: "game");
        }
    }
}
