using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_device",
                columns: table => new
                {
                    device_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    device_token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    device_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ios"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("user_device_pkey", x => x.device_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_device_device_token",
                table: "user_device",
                column: "device_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_device_user_id_is_active",
                table: "user_device",
                columns: new[] { "user_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_device");
        }
    }
}
