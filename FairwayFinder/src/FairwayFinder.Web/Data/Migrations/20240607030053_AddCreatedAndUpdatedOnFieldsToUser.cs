using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAndUpdatedOnFieldsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true,
                defaultValue: new DateTime(2024, 6, 7, 3, 0, 53, 462, DateTimeKind.Utc).AddTicks(1889));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOn",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true,
                defaultValue: new DateTime(2024, 6, 7, 3, 0, 53, 462, DateTimeKind.Utc).AddTicks(2146));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UpdatedOn",
                table: "AspNetUsers");
        }
    }
}
