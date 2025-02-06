using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Web.Migrations
{
    /// <inheritdoc />
    public partial class DropScoreType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE score
                DROP COLUMN IF EXISTS score_type;
            ");
        }
    }
}
