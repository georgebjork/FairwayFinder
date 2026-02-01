using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMoreFieldsToRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE round
                ADD COLUMN score INTEGER NOT NULL DEFAULT 0,
                ADD COLUMN score_out INTEGER NOT NULL DEFAULT 0,
                ADD COLUMN score_in INTEGER NOT NULL DEFAULT 0,
                ADD COLUMN user_id TEXT NOT NULL DEFAULT 'unknown';

                
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
