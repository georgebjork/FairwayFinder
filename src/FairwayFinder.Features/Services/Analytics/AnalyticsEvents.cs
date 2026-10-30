namespace FairwayFinder.Features.Services.Analytics;

/// <summary>
/// Every server-side event name, in one place so a typo cannot split one event into two in
/// PostHog. snake_case, past tense: the thing that happened.
/// </summary>
public static class AnalyticsEvents
{
    // ── Account ──
    /// <summary>The golfer used the app today. At most one per golfer per UTC day; see <see cref="ActiveGolferTracker"/>.</summary>
    public const string AppActive = "app_active";
    public const string UserSignedUp = "user_signed_up";
    public const string UserLoggedIn = "user_logged_in";
    public const string PushDeviceRegistered = "push_device_registered";

    // ── Rounds ──
    public const string RoundStarted = "round_started";
    public const string RoundCompleted = "round_completed";
    public const string RoundEdited = "round_edited";
    public const string RoundDeleted = "round_deleted";

    // ── Stats ──
    public const string StatsViewed = "stats_viewed";

    // ── Games ──
    public const string GameCreated = "game_created";
    public const string GameJoined = "game_joined";
    public const string GameStarted = "game_started";
    public const string GameCompleted = "game_completed";
    public const string GameAbandoned = "game_abandoned";

    // ── Friends ──
    public const string FriendRequestSent = "friend_request_sent";
    public const string FriendRequestAccepted = "friend_request_accepted";
    public const string FriendActivityViewed = "friend_activity_viewed";

    /// <summary>Property names shared across events, so the same fact is filterable everywhere.</summary>
    public static class Props
    {
        public const string CourseId = "course_id";
        public const string TeeboxId = "teebox_id";
        public const string RoundId = "round_id";
        public const string Holes = "holes";
        public const string Score = "score";
        public const string ToPar = "to_par";
        public const string Entry = "entry";
        public const string ShotTracking = "shot_tracking";
        public const string HoleStats = "hole_stats";
        public const string GameId = "game_id";
        public const string GameType = "game_type";
        public const string Players = "players";
        public const string AutoAccepted = "auto_accepted";
        public const string Scope = "scope";
        public const string BaselineLevel = "baseline_level";
        public const string Filtered = "filtered";
        public const string View = "view";
    }

    /// <summary>Which stats screen the golfer opened.</summary>
    public static class StatsScopes
    {
        public const string Overall = "overall";
        public const string Course = "course";
        public const string CourseHoles = "course_holes";
    }

    /// <summary>What the golfer looked at on a friend's profile.</summary>
    public static class FriendViews
    {
        public const string Rounds = "rounds";
        public const string Round = "round";
        public const string Stats = "stats";
    }

    /// <summary>How a round reached the server.</summary>
    public static class EntryModes
    {
        /// <summary>Hole by hole through <c>RoundEntryService</c>, as the iOS app logs a round.</summary>
        public const string HoleByHole = "hole_by_hole";

        /// <summary>The whole card in one deprecated <c>POST /api/rounds</c>.</summary>
        public const string FullCard = "full_card";
    }
}
