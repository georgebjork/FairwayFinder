using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApiRequestLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_request_log",
                columns: table => new
                {
                    api_request_log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    path = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    route_template = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    query_string = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    duration_ms = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    user_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("api_request_log_pkey", x => x.api_request_log_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_api_request_log_status_code",
                table: "api_request_log",
                column: "status_code");

            migrationBuilder.CreateIndex(
                name: "ix_api_request_log_timestamp",
                table: "api_request_log",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_api_request_log_user_id",
                table: "api_request_log",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_request_log");
        }
    }
}
