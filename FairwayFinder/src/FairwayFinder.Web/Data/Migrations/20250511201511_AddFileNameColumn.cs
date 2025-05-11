using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFileNameColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE profile_document
                ADD COLUMN file_name TEXT NOT NULL DEFAULT 'unknown';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
