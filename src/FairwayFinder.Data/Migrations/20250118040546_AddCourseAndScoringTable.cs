using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseAndScoringTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE teebox(
                    teebox_id BIGSERIAL NOT NULL PRIMARY KEY,
                    course_id BIGINT NOT NULL,
                    teebox_name TEXT NOT NULL,
                    par BIGINT NOT NULL,
                    rating DECIMAL(8, 2) NOT NULL,
                    slope BIGINT NOT NULL,
                    yardage_out BIGINT NOT NULL,
                    yardage_in BIGINT NOT NULL,
                    yardage_total BIGINT NOT NULL,
                    is_nine_hole BOOLEAN NOT NULL,
                    is_womens BOOLEAN NOT NULL,
                    created_by TEXT NOT NULL,
                    created_on DATE NOT NULL,
                    updated_by TEXT NOT NULL,
                    updated_on DATE NOT NULL
                );
                
                CREATE TABLE score(
                    score_id BIGSERIAL NOT NULL PRIMARY KEY,
                    round_id BIGINT NOT NULL,
                    hole_id BIGINT NOT NULL,
                    hole_score SMALLINT NOT NULL,
                    score_type TEXT NOT NULL,
                    user_id TEXT NOT NULL,
                    created_by TEXT NOT NULL,
                    created_on DATE NOT NULL,
                    updated_by TEXT NOT NULL,
                    updated_on DATE NOT NULL
                );

                CREATE TABLE hole(
                    hole_id BIGSERIAL NOT NULL PRIMARY KEY,
                    teebox_id BIGINT NOT NULL,
                    course_id BIGINT NOT NULL,
                    hole_number BIGINT NOT NULL,
                    yardage BIGINT NOT NULL,
                    handicap BIGINT NOT NULL,
                    par BIGINT NOT NULL,
                    created_by TEXT NOT NULL,
                    created_on DATE NOT NULL,
                    updated_by TEXT NOT NULL,
                    updated_on DATE NOT NULL
                );

                CREATE TABLE round(
                    round_id BIGSERIAL NOT NULL PRIMARY KEY,
                    course_id BIGINT NOT NULL,
                    teebox_id BIGINT NOT NULL,
                    date_played DATE NOT NULL,
                    created_by TEXT NOT NULL,
                    created_on DATE NOT NULL,
                    updated_by TEXT NOT NULL,
                    updated_on DATE NOT NULL
                );

                CREATE TABLE stats(
                    stat_id BIGSERIAL NOT NULL PRIMARY KEY,
                    score_id BIGINT NOT NULL,
                    hit_fairway BOOLEAN NULL,
                    miss_fairway_type TEXT NULL,
                    hit_green BOOLEAN NULL,
                    miss_green_type TEXT NULL,
                    number_of_putts SMALLINT NULL,
                    created_by TEXT NOT NULL,
                    created_on DATE NOT NULL,
                    updated_by TEXT NOT NULL,
                    updated_on DATE NOT NULL
                );
              
                CREATE TABLE course(
                    course_id BIGSERIAL NOT NULL PRIMARY KEY,
                    course_name TEXT NOT NULL,
                    address TEXT NULL,
                    phone_number TEXT NULL,
                    created_by TEXT NOT NULL,
                    created_on DATE NOT NULL,
                    updated_by TEXT NOT NULL,
                    updated_on DATE NOT NULL
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
