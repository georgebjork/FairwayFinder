using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFrontNineBackNineFullRoundBoolsToRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE round
                ADD COLUMN full_round BOOLEAN NOT NULL DEFAULT TRUE;

                ALTER TABLE round
                ADD COLUMN front_nine BOOLEAN NOT NULL DEFAULT FALSE;

                ALTER TABLE round
                ADD COLUMN back_nine BOOLEAN NOT NULL DEFAULT FALSE;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedOn",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2025, 3, 11, 2, 28, 49, 47, DateTimeKind.Utc).AddTicks(7040),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2025, 3, 17, 3, 58, 22, 766, DateTimeKind.Utc).AddTicks(8040));

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedOn",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(2025, 3, 11, 2, 28, 49, 47, DateTimeKind.Utc).AddTicks(6780),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValue: new DateTime(2025, 3, 17, 3, 58, 22, 766, DateTimeKind.Utc).AddTicks(7750));
        }
    }
}
