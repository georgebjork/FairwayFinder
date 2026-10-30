namespace FairwayFinder.Features.Services.Analytics;

/// <summary>
/// Every server-side event name, in one place so a typo cannot split one event into two in
/// PostHog. snake_case, past tense: the thing that happened.
/// </summary>
public static class AnalyticsEvents
{
    // ── Account ──
    public const string UserSignedUp = "user_signed_up";
    public const string UserLoggedIn = "user_logged_in";
    public const string PushDeviceRegistered = "push_device_registered";

    // ── Rounds ──
    public const string RoundStarted = "round_started";
    public const string RoundCompleted = "round_completed";

    // ── Games ──
    public const string GameCreated = "game_created";
    public const string GameJoined = "game_joined";
    public const string GameStarted = "game_started";
    public const string GameCompleted = "game_completed";
    public const string GameAbandoned = "game_abandoned";

    // ── Friends ──
    public const string FriendRequestSent = "friend_request_sent";
    public const string FriendRequestAccepted = "friend_request_accepted";

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
