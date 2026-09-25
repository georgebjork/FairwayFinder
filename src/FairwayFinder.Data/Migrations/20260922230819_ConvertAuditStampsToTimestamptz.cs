using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <summary>
    /// Widens every audit stamp from <c>date</c> to <c>timestamptz</c>. They were dates, so two rows
    /// created the same day could not be ordered and <c>updated_on</c> could not say when.
    /// </summary>
    /// <remarks>
    /// The USING clauses are not decoration. Postgres has an <em>implicit</em> date → timestamptz
    /// cast, so EF's generated bare <c>ALTER COLUMN ... TYPE timestamptz</c> succeeds — but it reads
    /// the stored midnight in the session's TimeZone, silently shifting every row by the server's UTC
    /// offset. <c>::timestamp AT TIME ZONE 'UTC'</c> pins midnight to UTC whatever the session says.
    /// That is why this body is hand-written rather than the scaffolded AlterColumn calls.
    ///
    /// Backfilled rows all land at midnight UTC; the historical time of day was never stored and is
    /// not recoverable. Real precision starts from here.
    /// </remarks>
    public partial class ConvertAuditStampsToTimestamptz : Migration
    {
        // Hardcoded rather than discovered from information_schema: the dev database also holds an
        // orphan `profile_document` table that belongs to no entity and no migration.
        private static readonly string[] AuditTables =
        [
            "course", "hole", "hole_stats", "round", "round_stats", "score", "shot", "teebox",
            "tgtr_player_map", "tgtr_course_map", "tgtr_teebox_map", "tgtr_round_map",
            "user_invitation", "user_profile", "friendship", "game", "game_participant",
            "game_hole_score"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both columns in one ALTER TABLE so each table is rewritten once, not twice.
            foreach (var table in AuditTables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table}
                        ALTER COLUMN created_on TYPE timestamptz
                            USING created_on::timestamp AT TIME ZONE 'UTC',
                        ALTER COLUMN updated_on TYPE timestamptz
                            USING updated_on::timestamp AT TIME ZONE 'UTC';
                    """);
            }

            // Event stamps outside the created_on/updated_on pair.
            migrationBuilder.Sql("""
                ALTER TABLE teebox
                    ALTER COLUMN archived_on TYPE timestamptz
                        USING archived_on::timestamp AT TIME ZONE 'UTC';
                """);

            migrationBuilder.Sql("""
                ALTER TABLE user_invitation
                    ALTER COLUMN claimed_on TYPE timestamptz
                        USING claimed_on::timestamp AT TIME ZONE 'UTC';
                """);

            // expires_on is a deadline, not a record of something that happened. `ExpiresOn >= today`
            // meant "live through the whole of that day", so midnight UTC would retroactively shorten
            // — or outright expire — every outstanding invite. End of day preserves the window each
            // invite was promised.
            migrationBuilder.Sql("""
                ALTER TABLE user_invitation
                    ALTER COLUMN expires_on TYPE timestamptz
                        USING (expires_on + INTERVAL '1 day')::timestamp AT TIME ZONE 'UTC';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossy: the time of day is discarded. `AT TIME ZONE 'UTC'` converts the instant back to
            // a UTC-naive timestamp before truncating, so the date matches what Up read in.
            migrationBuilder.Sql("""
                ALTER TABLE user_invitation
                    ALTER COLUMN expires_on TYPE date
                        USING ((expires_on AT TIME ZONE 'UTC')::date - 1);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE user_invitation
                    ALTER COLUMN claimed_on TYPE date
                        USING (claimed_on AT TIME ZONE 'UTC')::date;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE teebox
                    ALTER COLUMN archived_on TYPE date
                        USING (archived_on AT TIME ZONE 'UTC')::date;
                """);

            foreach (var table in AuditTables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table}
                        ALTER COLUMN created_on TYPE date
                            USING (created_on AT TIME ZONE 'UTC')::date,
                        ALTER COLUMN updated_on TYPE date
                            USING (updated_on AT TIME ZONE 'UTC')::date;
                    """);
            }
        }
    }
}
