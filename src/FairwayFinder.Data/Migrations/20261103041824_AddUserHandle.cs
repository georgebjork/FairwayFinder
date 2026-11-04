using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <summary>
    /// Adds the golfer's username (<c>AspNetUsers."Handle"</c>) and gives every existing account one.
    ///
    /// Hand-edited from the scaffold, which added the column NOT NULL with a <c>''</c> default and
    /// would then have failed the unique index on the second user. Instead: add it nullable, backfill,
    /// then tighten. The backfill mirrors <c>UsernameRules.BuildBase</c> and
    /// <c>UsernameService.GenerateDefaultAsync</c> (first + last name, lowercased, letters and digits
    /// only, at most 15 chars, else "golfer"; a random 2–4 digit suffix when taken or reserved), so old
    /// and new accounts look alike. Oldest accounts go first and so get the un-suffixed names. The
    /// reserved list must match <c>UsernameRules.Reserved</c>.
    /// </summary>
    public partial class AddUserHandle : Migration
    {
        /// <summary>
        /// Gives every user whose <c>"Handle"</c> is null a default username. Public so
        /// <c>UsernameBackfillTests</c> can run the very same SQL against real rows.
        /// </summary>
        public const string BackfillSql = """
            DO $$
            DECLARE
                r          record;
                base       text;
                candidate  text;
                attempt    int;
                digits     int;
                reserved   text[] := ARRAY[
                    'admin', 'administrator', 'api', 'fairwayfinder', 'fairway_finder', 'help',
                    'me', 'moderator', 'null', 'official', 'root', 'staff', 'support', 'system',
                    'undefined'];
            BEGIN
                FOR r IN
                    SELECT "Id", "FirstName", "LastName"
                    FROM "AspNetUsers"
                    WHERE "Handle" IS NULL
                    ORDER BY "CreatedOn", "Id"
                LOOP
                    base := left(
                        regexp_replace(
                            translate(
                                lower(coalesce(r."FirstName", '') || coalesce(r."LastName", '')),
                                'àáâãäåçèéêëìíîïñòóôõöùúûüýÿ',
                                'aaaaaaceeeeiiiinooooouuuuyy'),
                            '[^a-z0-9]', '', 'g'),
                        15);
                    IF length(base) < 3 THEN
                        base := 'golfer';
                    END IF;

                    candidate := base;
                    attempt := 0;
                    WHILE candidate = ANY (reserved)
                          OR EXISTS (SELECT 1 FROM "AspNetUsers" WHERE "Handle" = candidate)
                    LOOP
                        attempt := attempt + 1;
                        digits := CASE WHEN attempt <= 3 THEN 2 WHEN attempt <= 6 THEN 3 ELSE 4 END;
                        candidate := base || (power(10, digits - 1)
                            + floor(random() * 9 * power(10, digits - 1)))::bigint::text;
                    END LOOP;

                    UPDATE "AspNetUsers" SET "Handle" = candidate WHERE "Id" = r."Id";
                END LOOP;
            END $$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Handle",
                table: "AspNetUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(BackfillSql);

            migrationBuilder.AlterColumn<string>(
                name: "Handle",
                table: "AspNetUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_aspnetusers_handle",
                table: "AspNetUsers",
                column: "Handle",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_aspnetusers_handle",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Handle",
                table: "AspNetUsers");
        }
    }
}
