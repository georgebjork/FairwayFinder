using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayFinder.IntegrationTests.Common;

public sealed record SeededCourse(long CourseId, long TeeboxId, string CourseName, IReadOnlyList<int> Pars)
{
    public int Par => Pars.Sum();
}

/// <summary>
/// Arranges rows the way the application itself would create them. Extend this rather than
/// building entities inline in tests, so a schema change is fixed in one place.
/// </summary>
public sealed class TestData(IServiceProvider services)
{
    public const string DefaultPassword = "Password123!";

    /// <summary>Par for holes 1-18: a standard 36/36, par 72.</summary>
    public static readonly int[] StandardPars = [4, 5, 3, 4, 4, 3, 5, 4, 4, 4, 3, 5, 4, 4, 3, 4, 5, 4];

    private int _sequence;

    /// <summary>
    /// Creates a confirmed user through <see cref="UserManager{TUser}"/> (so the password hash and
    /// security stamp are real), assigns roles, and creates the profile registration would.
    /// </summary>
    public async Task<ApplicationUser> CreateUserAsync(
        string? email = null,
        string password = DefaultPassword,
        string firstName = "Test",
        string? lastName = null,
        params string[] roles)
    {
        var n = Interlocked.Increment(ref _sequence);
        email ??= $"golfer{n}-{Guid.NewGuid():N}@test.fairwayfinder.pro";

        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName ?? $"Golfer{n}",
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join(" ", created.Errors.Select(e => e.Description)));

        if (roles.Length > 0)
        {
            var added = await userManager.AddToRolesAsync(user, roles);
            if (!added.Succeeded)
                throw new InvalidOperationException(string.Join(" ", added.Errors.Select(e => e.Description)));
        }

        await scope.ServiceProvider.GetRequiredService<IProfileService>().GetOrCreateProfileAsync(user.Id);

        return user;
    }

    public Task<ApplicationUser> CreateAdminAsync(string? email = null) =>
        CreateUserAsync(email, roles: ApplicationRoles.Admin);

    /// <summary>A course with one teebox and its holes (18 by default, or a nine-hole teebox).</summary>
    public async Task<SeededCourse> CreateCourseAsync(
        string? courseName = null,
        bool nineHole = false,
        string teeboxName = "Blue",
        string createdBy = "integration-tests")
    {
        courseName ??= $"Test Course {Interlocked.Increment(ref _sequence)}";
        var pars = StandardPars.Take(nineHole ? 9 : 18).ToArray();

        await using var db = CreateDbContext();

        var course = new Course { CourseName = courseName, CreatedBy = createdBy, UpdatedBy = createdBy };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var teebox = new Teebox
        {
            CourseId = course.CourseId,
            TeeboxName = teeboxName,
            Par = pars.Sum(),
            Rating = 71.5m,
            Slope = 130,
            IsNineHole = nineHole,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
        };
        db.Teeboxes.Add(teebox);
        await db.SaveChangesAsync();

        db.Holes.AddRange(pars.Select((par, i) => new Hole
        {
            TeeboxId = teebox.TeeboxId,
            CourseId = course.CourseId,
            HoleNumber = i + 1,
            Par = par,
            Yardage = 400,
            Handicap = i + 1,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
        }));
        await db.SaveChangesAsync();

        return new SeededCourse(course.CourseId, teebox.TeeboxId, courseName, pars);
    }

    /// <summary>
    /// A posted 18-hole round, written through <see cref="IRoundService"/> exactly as a submit
    /// would be. Each hole is scored at par plus <paramref name="overPar"/>.
    /// </summary>
    public async Task<long> CreateCompletedRoundAsync(
        string userId,
        SeededCourse course,
        int overPar = 0,
        DateOnly? datePlayed = null)
    {
        List<Hole> holes;
        await using (var db = CreateDbContext())
        {
            holes = await db.Holes
                .Where(h => h.TeeboxId == course.TeeboxId && !h.IsDeleted)
                .OrderBy(h => h.HoleNumber)
                .ToListAsync();
        }

        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRoundService>().CreateRoundAsync(new CreateRoundRequest
        {
            UserId = userId,
            CourseId = course.CourseId,
            TeeboxId = course.TeeboxId,
            DatePlayed = datePlayed ?? new DateOnly(2026, 7, 1),
            FullRound = holes.Count == 18,
            FrontNine = holes.Count == 9,
            Holes = holes.Select(h => new HoleScoreEntry
            {
                HoleId = h.HoleId,
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Score = (short)(h.Par + overPar),
            }).ToList(),
        });
    }

    /// <summary>A game in setup, hosted by <paramref name="hostUserId"/>, created through <see cref="IGameService"/>.</summary>
    public async Task<GameStateResponse> CreateGameAsync(string hostUserId, SeededCourse course, GameType gameType = GameType.MatchPlay)
    {
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IGameService>().CreateGameAsync(new CreateGameRequest
        {
            GameType = gameType,
            CourseId = course.CourseId,
            TeeboxId = course.TeeboxId,
            DatePlayed = new DateOnly(2026, 7, 1),
        }, hostUserId);

        if (!result.IsOk)
            throw new InvalidOperationException($"Creating a game failed: {result.Status} {result.Detail}");

        return result.Value!;
    }

    /// <summary>An unclaimed invitation, as the admin console or invite endpoint would create it.</summary>
    public async Task<UserInvitation> CreateInvitationAsync(
        string email,
        string sentByUserId = "integration-tests",
        DateTime? expiresOn = null)
    {
        await using var db = CreateDbContext();

        var invitation = new UserInvitation
        {
            InvitationIdentifier = Guid.NewGuid().ToString("N"),
            SentToEmail = email,
            SentByUser = sentByUserId,
            ExpiresOn = expiresOn ?? DateTime.UtcNow.AddDays(7),
            CreatedBy = sentByUserId,
            UpdatedBy = sentByUserId,
        };
        db.UserInvitations.Add(invitation);
        await db.SaveChangesAsync();

        return invitation;
    }

    public ApplicationDbContext CreateDbContext() =>
        services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext();
}
