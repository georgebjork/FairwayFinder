using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedColumnToAllTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE teebox
                ADD is_deleted BOOLEAN NOT NULL DEFAULT false;
                
                ALTER TABLE score
                ADD is_deleted BOOLEAN NOT NULL DEFAULT false;

                ALTER TABLE hole
                ADD is_deleted BOOLEAN NOT NULL DEFAULT false;    

                ALTER TABLE round
                ADD is_deleted BOOLEAN NOT NULL DEFAULT false;   

                ALTER TABLE stats
                ADD is_deleted BOOLEAN NOT NULL DEFAULT false;    
              
                ALTER TABLE course
                ADD is_deleted BOOLEAN NOT NULL DEFAULT false;    

            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
