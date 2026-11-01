using FairwayFinder.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FairwayFinder.Data.Migrations
{
    /// <summary>
    /// Data-only: gives every teebox still on <c>teebox_group_id = 0</c> a real lineage. The
    /// GolfCourseAPI importer used to insert teeboxes without one, and versioning such a tee carried
    /// the 0 forward to the new version.
    ///
    /// There is no parent link on a version, so lineages are rebuilt the way they were made: a
    /// version keeps its source's course, name, gender and hole count. Within each such set the
    /// oldest teebox is the root and every 0-group row takes its id, which for an un-versioned tee
    /// is simply its own. No model change, so the snapshot is untouched.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261031120100_BackfillTeeboxGroupIds")]
    public partial class BackfillTeeboxGroupIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE teebox t
                SET teebox_group_id = roots.root_id
                FROM (
                    SELECT teebox_id,
                           MIN(teebox_id) OVER (
                               PARTITION BY course_id, lower(teebox_name), is_womens, is_nine_hole) AS root_id
                    FROM teebox
                    WHERE teebox_group_id = 0
                ) roots
                WHERE t.teebox_id = roots.teebox_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible on purpose: 0 was never a valid lineage.
        }
    }
}
