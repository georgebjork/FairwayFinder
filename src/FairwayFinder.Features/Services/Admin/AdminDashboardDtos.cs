namespace FairwayFinder.Features.Services.Admin;

/// <summary>
/// Top-level aggregate of every metric shown on the admin overview dashboard.
/// Built from a single point-in-time snapshot so all groups are internally consistent.
/// System health (HealthCheckService) and live Blazor connections (CircuitTrackingService)
/// are composed in the Web layer, not here.
/// </summary>
public class DashboardMetricsDto
{
    public GrowthMetricsDto Growth { get; set; } = new();
    public ActivityMetricsDto Activity { get; set; } = new();
    public InviteMetricsDto Invites { get; set; } = new();
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single bucket in a time series. <see cref="PeriodStart"/> is the first day of the bucket
/// (used for sorting/charting); <see cref="Label"/> is the display string.
/// </summary>
public class TimeSeriesPoint
{
    public DateOnly PeriodStart { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

// ── Group 1: Growth & users ──
public class GrowthMetricsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }            // >= 1 non-deleted round in last 30 days
    public int NewUsersLast30Days { get; set; }     // signups in the last 30 days
    public int ActivatedUsers { get; set; }         // have logged >= 1 round ever (non-deleted)
    public int AdminCount { get; set; }
    public int EmailConfirmedCount { get; set; }
    public double EmailConfirmedPercent { get; set; } // 0..100, rounded
    public List<TimeSeriesPoint> SignupsByMonth { get; set; } = new();
    public List<TimeSeriesPoint> SignupsByWeek { get; set; } = new();
    public List<RecentUserDto> RecentlyJoined { get; set; } = new();

    // Admin-facing derived signals.
    public int DormantUsers => Math.Max(0, TotalUsers - ActivatedUsers); // signed up, never logged a round
    public double ActivationRatePercent => TotalUsers == 0 ? 0 : Math.Round(100.0 * ActivatedUsers / TotalUsers, 1);
    public double EngagementRatePercent => TotalUsers == 0 ? 0 : Math.Round(100.0 * ActiveUsers / TotalUsers, 1);
}

public class RecentUserDto
{
    public string Id { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Email { get; set; } = string.Empty;
    public bool IsEmailConfirmed { get; set; }
    public DateTime CreatedOn { get; set; }
}

// ── Group 2: Activity & content ──
public class ActivityMetricsDto
{
    public int TotalRounds { get; set; }              // non-deleted
    public int RoundsLast7Days { get; set; }
    public int RoundsLast30Days { get; set; }
    public int TotalCourses { get; set; }
    public double ShotTrackingAdoptionPercent { get; set; } // shot-tracked rounds / total rounds
    public List<TimeSeriesPoint> RoundsByMonth { get; set; } = new();
    public List<TimeSeriesPoint> RoundsByWeek { get; set; } = new();
    public List<MostPlayedCourseDto> MostPlayedCourses { get; set; } = new();
}

public class MostPlayedCourseDto
{
    public long CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int RoundCount { get; set; }
}

// ── Group 3: Invites ──
public class InviteMetricsDto
{
    public int PendingCount { get; set; }   // !IsDeleted, ClaimedOn == null, ExpiresOn >= today
    public int ClaimedCount { get; set; }   // !IsDeleted, ClaimedOn != null
    public int ExpiredCount { get; set; }   // !IsDeleted, ClaimedOn == null, ExpiresOn < today
    public double ClaimRatePercent { get; set; } // Claimed / non-deleted total, rounded
    public List<RecentInviteDto> RecentInvites { get; set; } = new();
}

public class RecentInviteDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateOnly CreatedOn { get; set; }
    public DateOnly ExpiresOn { get; set; }
    public DateOnly? ClaimedOn { get; set; }
    public string Status { get; set; } = string.Empty; // "Pending" | "Claimed" | "Expired"
}
