using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <inheritdoc />
    public partial class RecreateHoleStatsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"


                DROP TABLE IF EXISTS hole_stats;

                CREATE TABLE hole_stats(
                    hole_stats_id BIGSERIAL NOT NULL PRIMARY KEY,
                    score_id BIGINT NOT NULL,
                    round_id BIGINT NOT NULL,
                    hole_id BIGINT NOT NULL,
                    hit_fairway BOOLEAN NULL,
                    miss_fairway_type INT NULL,
                    hit_green BOOLEAN NULL,
                    miss_green_type INT NULL,
                    number_of_putts SMALLINT NULL,
                    approach_yardage INT NULL,
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
