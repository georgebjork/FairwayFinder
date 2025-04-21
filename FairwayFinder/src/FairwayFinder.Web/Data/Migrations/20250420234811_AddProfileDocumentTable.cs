using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileDocumentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE profile_document (
                  id SERIAL PRIMARY KEY NOT NULL,
                  document_id TEXT NOT NULL,
                  route TEXT NOT NULL,
                  file_url TEXT NOT NULL,
                  user_id TEXT NOT NULL,
                  is_deleted BOOLEAN NOT NULL,
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
