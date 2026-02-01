using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUserNamesToGuidsInCrudFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DO $$ 
DECLARE 
    r RECORD;
BEGIN
    FOR r IN 
        (SELECT table_name FROM information_schema.columns 
         WHERE column_name IN ('created_by', 'updated_by') 
         GROUP BY table_name) 
    LOOP
        EXECUTE format(
            'UPDATE %I as t
             SET created_by = u.""Id""
             FROM ""AspNetUsers"" as u
             WHERE t.created_by = u.""UserName"";', r.table_name
        );

        EXECUTE format(
            'UPDATE %I as t
             SET updated_by = u.""Id""
             FROM ""AspNetUsers"" as u
             WHERE t.updated_by = u.""UserName"";', r.table_name
        );
    END LOOP;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
           
        }
    }
}
