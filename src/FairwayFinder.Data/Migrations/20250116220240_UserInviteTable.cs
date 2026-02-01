using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserInviteTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE user_invitation (
                  id SERIAL PRIMARY KEY,
                  invitation_identifier TEXT NOT NULL,
                  sent_to_email TEXT NOT NULL,
                  sent_by_user TEXT NOT NULL,
                  is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                  claimed_on DATE,
                  expires_on DATE NOT NULL,
                  created_on DATE NOT NULL,
                  created_by TEXT NOT NULL,
                  updated_on DATE NOT NULL,
                  updated_by TEXT NOT NULL
                );

            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
