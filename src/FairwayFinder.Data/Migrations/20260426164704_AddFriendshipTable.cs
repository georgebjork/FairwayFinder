using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendshipTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "friendship",
                columns: table => new
                {
                    friendship_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    requester_user_id = table.Column<string>(type: "text", nullable: false),
                    addressee_user_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    responded_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    updated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("friendship_pkey", x => x.friendship_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_friendship_addressee_status",
                table: "friendship",
                columns: new[] { "addressee_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_friendship_requester_status",
                table: "friendship",
                columns: new[] { "requester_user_id", "status" });

            migrationBuilder.Sql(@"
                ALTER TABLE friendship
                ADD CONSTRAINT ck_friendship_no_self
                CHECK (requester_user_id <> addressee_user_id);
            ");

            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ux_friendship_pair_active
                ON friendship (LEAST(requester_user_id, addressee_user_id), GREATEST(requester_user_id, addressee_user_id))
                WHERE is_deleted = false AND status IN (0, 1);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "friendship");
        }
    }
}
