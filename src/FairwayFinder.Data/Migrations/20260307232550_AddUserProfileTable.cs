using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_profile",
                columns: table => new
                {
                    user_profile_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    public_identifier = table.Column<Guid>(type: "uuid", nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_profile_pkey", x => x.user_profile_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_profile_public_identifier",
                table: "user_profile",
                column: "public_identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_profile_user_id",
                table: "user_profile",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_profile");
        }
    }
}
