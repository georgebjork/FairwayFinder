using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Tests.Helpers;
using FairwayFinder.Shared;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Tests;

/// <summary>
/// Covers <c>AuditStampInterceptor</c>: services no longer set CreatedOn/UpdatedOn by hand, so
/// these assert the interceptor actually does it — and that the stamps are UTC instants with
/// enough precision to order two rows written in the same second.
/// </summary>
public class AuditStampTests
{
    private static Course NewCourse(string name = "Test Course") => new()
    {
        CourseName = name,
        ClubName = name,
        CreatedBy = "user-1",
        UpdatedBy = "user-1",
        IsDeleted = false
    };

    [Fact]
    public async Task Insert_stamps_both_audit_columns_even_though_the_caller_never_sets_them()
    {
        var factory = new InMemoryDbContextFactory(nameof(Insert_stamps_both_audit_columns_even_though_the_caller_never_sets_them));

        await using var db = factory.CreateDbContext();
        var course = NewCourse();
        Assert.Equal(default, course.CreatedOn);

        db.Courses.Add(course);
        await db.SaveChangesAsync();

        Assert.NotEqual(default, course.CreatedOn);
        Assert.Equal(course.CreatedOn, course.UpdatedOn);
    }

    [Fact]
    public async Task Stamps_are_utc_kinded_because_Npgsql_rejects_anything_else_for_timestamptz()
    {
        var factory = new InMemoryDbContextFactory(nameof(Stamps_are_utc_kinded_because_Npgsql_rejects_anything_else_for_timestamptz));

        await using var db = factory.CreateDbContext();
        var course = NewCourse();
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        Assert.Equal(DateTimeKind.Utc, course.CreatedOn.Kind);
        Assert.Equal(DateTimeKind.Utc, course.UpdatedOn.Kind);
    }

    [Fact]
    public async Task Update_advances_UpdatedOn_and_leaves_CreatedOn_alone()
    {
        var factory = new InMemoryDbContextFactory(nameof(Update_advances_UpdatedOn_and_leaves_CreatedOn_alone));

        long courseId;
        DateTime createdOn;

        await using (var db = factory.CreateDbContext())
        {
            var course = NewCourse();
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            courseId = course.CourseId;
            createdOn = course.CreatedOn;
        }

        await using (var db = factory.CreateDbContext())
        {
            var course = await db.Courses.SingleAsync(c => c.CourseId == courseId);
            course.City = "Duluth";
            await db.SaveChangesAsync();

            Assert.Equal(createdOn, course.CreatedOn);
            Assert.True(course.UpdatedOn >= createdOn);
        }
    }

    [Fact]
    public async Task A_stale_CreatedOn_on_an_update_cannot_overwrite_the_stored_one()
    {
        var factory = new InMemoryDbContextFactory(nameof(A_stale_CreatedOn_on_an_update_cannot_overwrite_the_stored_one));

        long courseId;
        DateTime createdOn;

        await using (var db = factory.CreateDbContext())
        {
            var course = NewCourse();
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            courseId = course.CourseId;
            createdOn = course.CreatedOn;
        }

        await using (var db = factory.CreateDbContext())
        {
            var course = await db.Courses.SingleAsync(c => c.CourseId == courseId);
            course.CreatedOn = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            await db.SaveChangesAsync();
        }

        await using (var db = factory.CreateDbContext())
        {
            var course = await db.Courses.SingleAsync(c => c.CourseId == courseId);
            Assert.Equal(createdOn, course.CreatedOn);
        }
    }

    [Fact]
    public async Task Two_rows_written_in_succession_are_orderable_which_date_columns_could_not_do()
    {
        var factory = new InMemoryDbContextFactory(nameof(Two_rows_written_in_succession_are_orderable_which_date_columns_could_not_do));

        await using var db = factory.CreateDbContext();

        var first = NewCourse("First");
        db.Courses.Add(first);
        await db.SaveChangesAsync();

        var second = NewCourse("Second");
        db.Courses.Add(second);
        await db.SaveChangesAsync();

        Assert.True(second.CreatedOn > first.CreatedOn,
            $"expected distinct, orderable stamps but got {first.CreatedOn:O} and {second.CreatedOn:O}");
    }

    /// <summary>
    /// Builds the model against the real Npgsql provider so column types resolve. No connection is
    /// opened — model building is offline — but the in-memory provider cannot answer
    /// <c>GetColumnType()</c> at all, so this has to be the Postgres one.
    /// </summary>
    private static ApplicationDbContext CreateNpgsqlModelContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only;Username=none;Password=none")
            .Options);

    /// <summary>
    /// Guards the bug this whole change fixed: a DateTime property that silently maps to something
    /// other than timestamptz. This is the check that would have caught the original
    /// <c>created_on DATE NOT NULL</c> in the hand-written 2025 schema migrations.
    /// </summary>
    [Fact]
    public void Every_DateTime_property_in_the_model_maps_to_timestamptz()
    {
        using var db = CreateNpgsqlModelContext();

        var offenders = db.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
            .Select(p => new { Name = $"{p.DeclaringType.ClrType.Name}.{p.Name}", Type = p.GetColumnType() })
            .Where(x => x.Type is not null && x.Type != "timestamp with time zone")
            .Select(x => $"{x.Name} -> {x.Type}")
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// The two genuine calendar dates stay <c>DateOnly</c>: a round is played on a day, not at an
    /// instant, and the client sends its own local date precisely so an evening round is not filed
    /// under tomorrow.
    /// </summary>
    [Fact]
    public void DatePlayed_stays_a_calendar_date()
    {
        using var db = CreateNpgsqlModelContext();

        Assert.Equal("date", db.Model.FindEntityType(typeof(Round))!.FindProperty(nameof(Round.DatePlayed))!.GetColumnType());
        Assert.Equal("date", db.Model.FindEntityType(typeof(Game))!.FindProperty(nameof(Game.DatePlayed))!.GetColumnType());
        Assert.Equal(typeof(DateOnly), db.Model.FindEntityType(typeof(Round))!.FindProperty(nameof(Round.DatePlayed))!.ClrType);
        Assert.Equal(typeof(DateOnly), db.Model.FindEntityType(typeof(Game))!.FindProperty(nameof(Game.DatePlayed))!.ClrType);
    }

    /// <summary>
    /// Every entity carrying the audit pair must implement <see cref="IAuditable"/>, or the
    /// interceptor silently skips it and the columns stay at default.
    /// </summary>
    [Fact]
    public void Every_entity_with_the_audit_pair_implements_IAuditable()
    {
        using var db = new InMemoryDbContextFactory(nameof(Every_entity_with_the_audit_pair_implements_IAuditable)).CreateDbContext();

        var unmarked = db.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(t => t.GetProperty(nameof(IAuditable.CreatedOn)) is not null
                        && t.GetProperty(nameof(IAuditable.UpdatedOn)) is not null
                        && !typeof(IAuditable).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(unmarked);
    }
}
