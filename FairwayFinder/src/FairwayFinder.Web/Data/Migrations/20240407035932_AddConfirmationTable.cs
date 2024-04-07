using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddConfirmationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE email_confirmation (
                    id SERIAL PRIMARY KEY,
                    user_id TEXT NOT NULL, 
                    confirmation_id TEXT NOT NULL,
                    sent_to_email TEXT NOT NULL,
                    is_confirmed BOOLEAN NOT NULL DEFAULT FALSE,
                    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
                    confirmed_on DATE,
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
