using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissTypeLookupTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE miss_type(
                    miss_type_id BIGSERIAL NOT NULL PRIMARY KEY,
                    miss_type TEXT NOT NULL
                );

                INSERT INTO miss_type (miss_type) VALUES ('Left');
                INSERT INTO miss_type (miss_type) VALUES ('Right');
                INSERT INTO miss_type (miss_type) VALUES ('Short');
                INSERT INTO miss_type (miss_type) VALUES ('Long');

            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            
        }
    }
}
