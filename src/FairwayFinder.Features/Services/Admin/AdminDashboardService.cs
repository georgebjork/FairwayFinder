using FairwayFinder.Data;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services.Admin;

/// <summary>
/// Aggregates the metrics shown on the admin overview dashboard. Reads a single point-in-time
/// snapshot (one DbContext, one "today") so all groups are consistent with each other.
/// Time-series bucketing is done in memory over small, windowed projections to avoid any
/// provider-specific date translation issues.
/// </summary>
public class AdminDashboardService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    UserManager<ApplicationUser> userManager)
{
    private const int RecentCount = 10;
    private const int TopCoursesCount = 5;
    private const int MonthWindow = 12;  // months of history for monthly charts
    private const int WeekWindow = 12;   // weeks of history for weekly charts

    public async Task<DashboardMetricsDto> GetDashboardMetricsAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return new DashboardMetricsDto
        {
            Growth = await GetGrowthMetricsAsync(db, today),
            Activity = await GetActivityMetricsAsync(db, today),
            Invites = await GetInviteMetricsAsync(db, today),
        };
    }

    // ── Group 1: Growth & users ──
    private async Task<GrowthMetricsDto> GetGrowthMetricsAsync(ApplicationDbContext db, DateOnly today)
    {
        var cutoff30 = today.AddDays(-30);

        var totalUsers = await db.Users.CountAsync();
        var emailConfirmed = await db.Users.CountAsync(u => u.EmailConfirmed);
        var adminCount = (await userManager.GetUsersInRoleAsync("Admin")).Count;

        var activeUsers = await db.Rounds
            .Where(r => !r.IsDeleted && r.DatePlayed >= cutoff30)
            .Select(r => r.UserId)
            .Distinct()
            .CountAsync();

        // Users who have ever logged a round (activation), and signups in the last 30 days.
        var activatedUsers = await db.Rounds
            .Where(r => !r.IsDeleted)
            .Select(r => r.UserId)
            .Distinct()
            .CountAsync();

        var cutoff30Utc = DateTime.SpecifyKind(cutoff30.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var newUsers30 = await db.Users.CountAsync(u => u.CreatedOn >= cutoff30Utc);

        var recentlyJoined = await db.Users
            .OrderByDescending(u => u.CreatedOn)
            .Take(RecentCount)
            .Select(u => new RecentUserDto
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                IsEmailConfirmed = u.EmailConfirmed,
                FirstName = u.FirstName,
                LastName = u.LastName,
                CreatedOn = u.CreatedOn
            })
            .ToListAsync();

        // Project the signup dates over the chart window and bucket in memory.
        var weekCutoff = StartOfWeek(today).AddDays(-7 * (WeekWindow - 1));
        var monthCutoff = FirstOfMonth(today).AddMonths(-(MonthWindow - 1));
        var earliest = weekCutoff < monthCutoff ? weekCutoff : monthCutoff;

        // CreatedOn is a `timestamptz` column — Npgsql requires the parameter be UTC-kinded.
        var earliestUtc = DateTime.SpecifyKind(earliest.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var signupDates = await db.Users
            .Where(u => u.CreatedOn >= earliestUtc)
            .Select(u => u.CreatedOn)
            .ToListAsync();
        var signupDateOnly = signupDates.Select(DateOnly.FromDateTime).ToList();

        return new GrowthMetricsDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            NewUsersLast30Days = newUsers30,
            ActivatedUsers = activatedUsers,
            AdminCount = adminCount,
            EmailConfirmedCount = emailConfirmed,
            EmailConfirmedPercent = totalUsers == 0 ? 0 : Math.Round(100.0 * emailConfirmed / totalUsers, 1),
            SignupsByMonth = BuildMonthlySeries(today, signupDateOnly),
            SignupsByWeek = BuildWeeklySeries(today, signupDateOnly),
            RecentlyJoined = recentlyJoined,
        };
    }

    // ── Group 2: Activity & content ──
    private async Task<ActivityMetricsDto> GetActivityMetricsAsync(ApplicationDbContext db, DateOnly today)
    {
        var cutoff7 = today.AddDays(-7);
        var cutoff30 = today.AddDays(-30);

        var totalRounds = await db.Rounds.CountAsync(r => !r.IsDeleted);
        var roundsLast7 = await db.Rounds.CountAsync(r => !r.IsDeleted && r.DatePlayed >= cutoff7);
        var roundsLast30 = await db.Rounds.CountAsync(r => !r.IsDeleted && r.DatePlayed >= cutoff30);
        var shotTracked = await db.Rounds.CountAsync(r => !r.IsDeleted && r.UsingShotTracking);
        var totalCourses = await db.Courses.CountAsync(c => !c.IsDeleted);

        // Top courses: a simple aggregate (no join), then resolve names in a second query.
        var topCourseCounts = await db.Rounds
            .Where(r => !r.IsDeleted)
            .GroupBy(r => r.CourseId)
            .Select(g => new { CourseId = g.Key, RoundCount = g.Count() })
            .OrderByDescending(x => x.RoundCount)
            .Take(TopCoursesCount)
            .ToListAsync();

        var topCourseIds = topCourseCounts.Select(x => x.CourseId).ToList();
        var courseNames = await db.Courses
            .Where(c => topCourseIds.Contains(c.CourseId))
            .Select(c => new { c.CourseId, c.CourseName })
            .ToListAsync();

        var mostPlayed = topCourseCounts
            .Select(x => new MostPlayedCourseDto
            {
                CourseId = x.CourseId,
                CourseName = courseNames.FirstOrDefault(c => c.CourseId == x.CourseId)?.CourseName ?? "Unknown",
                RoundCount = x.RoundCount
            })
            .ToList();

        // Project round dates over the chart window and bucket in memory.
        var weekCutoff = StartOfWeek(today).AddDays(-7 * (WeekWindow - 1));
        var monthCutoff = FirstOfMonth(today).AddMonths(-(MonthWindow - 1));
        var earliest = weekCutoff < monthCutoff ? weekCutoff : monthCutoff;

        var roundDates = await db.Rounds
            .Where(r => !r.IsDeleted && r.DatePlayed >= earliest)
            .Select(r => r.DatePlayed)
            .ToListAsync();

        return new ActivityMetricsDto
        {
            TotalRounds = totalRounds,
            RoundsLast7Days = roundsLast7,
            RoundsLast30Days = roundsLast30,
            TotalCourses = totalCourses,
            ShotTrackingAdoptionPercent = totalRounds == 0 ? 0 : Math.Round(100.0 * shotTracked / totalRounds, 1),
            RoundsByMonth = BuildMonthlySeries(today, roundDates),
            RoundsByWeek = BuildWeeklySeries(today, roundDates),
            MostPlayedCourses = mostPlayed,
        };
    }

    // ── Group 3: Invites ──
    private async Task<InviteMetricsDto> GetInviteMetricsAsync(ApplicationDbContext db, DateOnly today)
    {
        var pending = await db.UserInvitations.CountAsync(i => !i.IsDeleted && i.ClaimedOn == null && i.ExpiresOn >= today);
        var claimed = await db.UserInvitations.CountAsync(i => !i.IsDeleted && i.ClaimedOn != null);
        var expired = await db.UserInvitations.CountAsync(i => !i.IsDeleted && i.ClaimedOn == null && i.ExpiresOn < today);
        var nonDeleted = pending + claimed + expired;

        var recentInvites = await db.UserInvitations
            .Where(i => !i.IsDeleted)
            .OrderByDescending(i => i.CreatedOn).ThenByDescending(i => i.Id)
            .Take(RecentCount)
            .Select(i => new RecentInviteDto
            {
                Id = i.Id,
                Email = i.SentToEmail,
                CreatedOn = i.CreatedOn,
                ExpiresOn = i.ExpiresOn,
                ClaimedOn = i.ClaimedOn,
            })
            .ToListAsync();

        // Compute status in memory (keeps the projection translatable).
        foreach (var i in recentInvites)
        {
            i.Status = i.ClaimedOn != null ? "Claimed"
                     : i.ExpiresOn < today ? "Expired" : "Pending";
        }

        return new InviteMetricsDto
        {
            PendingCount = pending,
            ClaimedCount = claimed,
            ExpiredCount = expired,
            ClaimRatePercent = nonDeleted == 0 ? 0 : Math.Round(100.0 * claimed / nonDeleted, 1),
            RecentInvites = recentInvites,
        };
    }

    // ── Time-series helpers (all bucketing/labeling done in memory) ──

    private static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);

    // Week starts Monday.
    private static DateOnly StartOfWeek(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    private static List<TimeSeriesPoint> BuildMonthlySeries(DateOnly today, IEnumerable<DateOnly> dates)
    {
        var counts = new Dictionary<DateOnly, int>();
        foreach (var d in dates)
        {
            var key = FirstOfMonth(d);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        var series = new List<TimeSeriesPoint>(MonthWindow);
        var start = FirstOfMonth(today).AddMonths(-(MonthWindow - 1));
        for (var i = 0; i < MonthWindow; i++)
        {
            var period = start.AddMonths(i);
            series.Add(new TimeSeriesPoint
            {
                PeriodStart = period,
                Label = period.ToString("MMM yy"),
                Count = counts.GetValueOrDefault(period)
            });
        }
        return series;
    }

    private static List<TimeSeriesPoint> BuildWeeklySeries(DateOnly today, IEnumerable<DateOnly> dates)
    {
        var counts = new Dictionary<DateOnly, int>();
        foreach (var d in dates)
        {
            var key = StartOfWeek(d);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        var series = new List<TimeSeriesPoint>(WeekWindow);
        var start = StartOfWeek(today).AddDays(-7 * (WeekWindow - 1));
        for (var i = 0; i < WeekWindow; i++)
        {
            var period = start.AddDays(7 * i);
            series.Add(new TimeSeriesPoint
            {
                PeriodStart = period,
                Label = period.ToString("MMM d"),
                Count = counts.GetValueOrDefault(period)
            });
        }
        return series;
    }
}
