using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecreateStatTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"

                DROP TABLE IF EXISTS stats;

                CREATE TABLE hole_stats(
                    hole_stats_id BIGSERIAL NOT NULL PRIMARY KEY,
                    score_id BIGINT NOT NULL,
                    round_id BIGINT NOT NULL,
                    hit_fairway BOOLEAN NULL,
                    miss_fairway_type TEXT NULL,
                    hit_green BOOLEAN NULL,
                    miss_green_type TEXT NULL,
                    number_of_putts SMALLINT NULL,
                    created_by TEXT NOT NULL,
                    created_on DATE NOT NULL,
                    updated_by TEXT NOT NULL,
                    updated_on DATE NOT NULL,
                    is_deleted BOOLEAN NOT NULL
                );


                CREATE TABLE round_stats(
                    round_stats_id BIGSERIAL NOT NULL PRIMARY KEY,
                    round_id BIGINT NOT NULL,
                    hole_in_one INT NOT NULL,
                    double_eagles INT NOT NULL,
                    eagles INT NOT NULL,
                    birdies INT NOT NULL,
                    pars INT NOT NULL,
                    bogies INT NOT NULL,
                    double_bogies INT NOT NULL,
                    triple_or_worse INT NOT NULL,
                    created_by TEXT NOT NULL,
                    created_on DATE NOT NULL,
                    updated_by TEXT NOT NULL,
                    updated_on DATE NOT NULL,
                    is_deleted BOOLEAN NOT NULL
                );

            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            
        }
    }
}
