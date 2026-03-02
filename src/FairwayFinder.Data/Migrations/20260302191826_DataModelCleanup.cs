using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class DataModelCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "yardage_total",
                table: "teebox",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "yardage_out",
                table: "teebox",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "yardage_in",
                table: "teebox",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "slope",
                table: "teebox",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "par",
                table: "teebox",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "miss_green_type",
                table: "hole_stats",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "miss_fairway_type",
                table: "hole_stats",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "yardage",
                table: "hole",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "par",
                table: "hole",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "hole_number",
                table: "hole",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "handicap",
                table: "hole",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "IX_tgtr_teebox_map_teebox_id",
                table: "tgtr_teebox_map",
                column: "teebox_id");

            migrationBuilder.CreateIndex(
                name: "IX_tgtr_round_map_round_id",
                table: "tgtr_round_map",
                column: "round_id");

            migrationBuilder.CreateIndex(
                name: "IX_tgtr_course_map_course_id",
                table: "tgtr_course_map",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_teebox_course_id",
                table: "teebox",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_score_hole_id",
                table: "score",
                column: "hole_id");

            migrationBuilder.CreateIndex(
                name: "IX_score_round_id",
                table: "score",
                column: "round_id");

            migrationBuilder.CreateIndex(
                name: "IX_round_stats_round_id",
                table: "round_stats",
                column: "round_id");

            migrationBuilder.CreateIndex(
                name: "IX_round_course_id",
                table: "round",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_round_teebox_id",
                table: "round",
                column: "teebox_id");

            migrationBuilder.CreateIndex(
                name: "IX_hole_stats_hole_id",
                table: "hole_stats",
                column: "hole_id");

            migrationBuilder.CreateIndex(
                name: "IX_hole_stats_miss_fairway_type",
                table: "hole_stats",
                column: "miss_fairway_type");

            migrationBuilder.CreateIndex(
                name: "IX_hole_stats_miss_green_type",
                table: "hole_stats",
                column: "miss_green_type");

            migrationBuilder.CreateIndex(
                name: "IX_hole_stats_round_id",
                table: "hole_stats",
                column: "round_id");

            migrationBuilder.CreateIndex(
                name: "IX_hole_stats_score_id",
                table: "hole_stats",
                column: "score_id");

            migrationBuilder.CreateIndex(
                name: "IX_hole_course_id",
                table: "hole",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "IX_hole_teebox_id",
                table: "hole",
                column: "teebox_id");

            migrationBuilder.AddForeignKey(
                name: "FK_hole_course_course_id",
                table: "hole",
                column: "course_id",
                principalTable: "course",
                principalColumn: "course_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_hole_teebox_teebox_id",
                table: "hole",
                column: "teebox_id",
                principalTable: "teebox",
                principalColumn: "teebox_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_hole_stats_hole_hole_id",
                table: "hole_stats",
                column: "hole_id",
                principalTable: "hole",
                principalColumn: "hole_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_hole_stats_miss_type_miss_fairway_type",
                table: "hole_stats",
                column: "miss_fairway_type",
                principalTable: "miss_type",
                principalColumn: "miss_type_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_hole_stats_miss_type_miss_green_type",
                table: "hole_stats",
                column: "miss_green_type",
                principalTable: "miss_type",
                principalColumn: "miss_type_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_hole_stats_round_round_id",
                table: "hole_stats",
                column: "round_id",
                principalTable: "round",
                principalColumn: "round_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_hole_stats_score_score_id",
                table: "hole_stats",
                column: "score_id",
                principalTable: "score",
                principalColumn: "score_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_round_course_course_id",
                table: "round",
                column: "course_id",
                principalTable: "course",
                principalColumn: "course_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_round_teebox_teebox_id",
                table: "round",
                column: "teebox_id",
                principalTable: "teebox",
                principalColumn: "teebox_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_round_stats_round_round_id",
                table: "round_stats",
                column: "round_id",
                principalTable: "round",
                principalColumn: "round_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_score_hole_hole_id",
                table: "score",
                column: "hole_id",
                principalTable: "hole",
                principalColumn: "hole_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_score_round_round_id",
                table: "score",
                column: "round_id",
                principalTable: "round",
                principalColumn: "round_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_teebox_course_course_id",
                table: "teebox",
                column: "course_id",
                principalTable: "course",
                principalColumn: "course_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tgtr_course_map_course_course_id",
                table: "tgtr_course_map",
                column: "course_id",
                principalTable: "course",
                principalColumn: "course_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tgtr_round_map_round_round_id",
                table: "tgtr_round_map",
                column: "round_id",
                principalTable: "round",
                principalColumn: "round_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tgtr_teebox_map_teebox_teebox_id",
                table: "tgtr_teebox_map",
                column: "teebox_id",
                principalTable: "teebox",
                principalColumn: "teebox_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_hole_course_course_id",
                table: "hole");

            migrationBuilder.DropForeignKey(
                name: "FK_hole_teebox_teebox_id",
                table: "hole");

            migrationBuilder.DropForeignKey(
                name: "FK_hole_stats_hole_hole_id",
                table: "hole_stats");

            migrationBuilder.DropForeignKey(
                name: "FK_hole_stats_miss_type_miss_fairway_type",
                table: "hole_stats");

            migrationBuilder.DropForeignKey(
                name: "FK_hole_stats_miss_type_miss_green_type",
                table: "hole_stats");

            migrationBuilder.DropForeignKey(
                name: "FK_hole_stats_round_round_id",
                table: "hole_stats");

            migrationBuilder.DropForeignKey(
                name: "FK_hole_stats_score_score_id",
                table: "hole_stats");

            migrationBuilder.DropForeignKey(
                name: "FK_round_course_course_id",
                table: "round");

            migrationBuilder.DropForeignKey(
                name: "FK_round_teebox_teebox_id",
                table: "round");

            migrationBuilder.DropForeignKey(
                name: "FK_round_stats_round_round_id",
                table: "round_stats");

            migrationBuilder.DropForeignKey(
                name: "FK_score_hole_hole_id",
                table: "score");

            migrationBuilder.DropForeignKey(
                name: "FK_score_round_round_id",
                table: "score");

            migrationBuilder.DropForeignKey(
                name: "FK_teebox_course_course_id",
                table: "teebox");

            migrationBuilder.DropForeignKey(
                name: "FK_tgtr_course_map_course_course_id",
                table: "tgtr_course_map");

            migrationBuilder.DropForeignKey(
                name: "FK_tgtr_round_map_round_round_id",
                table: "tgtr_round_map");

            migrationBuilder.DropForeignKey(
                name: "FK_tgtr_teebox_map_teebox_teebox_id",
                table: "tgtr_teebox_map");

            migrationBuilder.DropIndex(
                name: "IX_tgtr_teebox_map_teebox_id",
                table: "tgtr_teebox_map");

            migrationBuilder.DropIndex(
                name: "IX_tgtr_round_map_round_id",
                table: "tgtr_round_map");

            migrationBuilder.DropIndex(
                name: "IX_tgtr_course_map_course_id",
                table: "tgtr_course_map");

            migrationBuilder.DropIndex(
                name: "IX_teebox_course_id",
                table: "teebox");

            migrationBuilder.DropIndex(
                name: "IX_score_hole_id",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_round_id",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_round_stats_round_id",
                table: "round_stats");

            migrationBuilder.DropIndex(
                name: "IX_round_course_id",
                table: "round");

            migrationBuilder.DropIndex(
                name: "IX_round_teebox_id",
                table: "round");

            migrationBuilder.DropIndex(
                name: "IX_hole_stats_hole_id",
                table: "hole_stats");

            migrationBuilder.DropIndex(
                name: "IX_hole_stats_miss_fairway_type",
                table: "hole_stats");

            migrationBuilder.DropIndex(
                name: "IX_hole_stats_miss_green_type",
                table: "hole_stats");

            migrationBuilder.DropIndex(
                name: "IX_hole_stats_round_id",
                table: "hole_stats");

            migrationBuilder.DropIndex(
                name: "IX_hole_stats_score_id",
                table: "hole_stats");

            migrationBuilder.DropIndex(
                name: "IX_hole_course_id",
                table: "hole");

            migrationBuilder.DropIndex(
                name: "IX_hole_teebox_id",
                table: "hole");

            migrationBuilder.AlterColumn<long>(
                name: "yardage_total",
                table: "teebox",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "yardage_out",
                table: "teebox",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "yardage_in",
                table: "teebox",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "slope",
                table: "teebox",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "par",
                table: "teebox",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "miss_green_type",
                table: "hole_stats",
                type: "integer",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "miss_fairway_type",
                table: "hole_stats",
                type: "integer",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "yardage",
                table: "hole",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "par",
                table: "hole",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "hole_number",
                table: "hole",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "handicap",
                table: "hole",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
