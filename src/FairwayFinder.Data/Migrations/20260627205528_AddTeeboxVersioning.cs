using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeeboxVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "archived_by",
                table: "teebox",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "archived_on",
                table: "teebox",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "teebox_group_id",
                table: "teebox",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Backfill: each existing teebox starts as its own lineage (group id = its own id).
            // New versions created via "save as new teebox" will share their source's group id.
            migrationBuilder.Sql("UPDATE teebox SET teebox_group_id = teebox_id WHERE teebox_group_id = 0;");

            migrationBuilder.CreateIndex(
                name: "ix_teebox_teebox_group_id",
                table: "teebox",
                column: "teebox_group_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_teebox_teebox_group_id",
                table: "teebox");

            migrationBuilder.DropColumn(
                name: "archived_by",
                table: "teebox");

            migrationBuilder.DropColumn(
                name: "archived_on",
                table: "teebox");

            migrationBuilder.DropColumn(
                name: "teebox_group_id",
                table: "teebox");
        }
    }
}
