using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FairwayFinder.Features.Diagnostics;

public static class FairwayFinderDiagnostics
{
    public const string RoundsName  = "FairwayFinder.Rounds";
    public const string StatsName   = "FairwayFinder.Stats";
    public const string ImportsName = "FairwayFinder.Imports";
    public const string EmailName   = "FairwayFinder.Email";

    public static readonly Meter RoundsMeter  = new(RoundsName,  "1.0.0");
    public static readonly Meter StatsMeter   = new(StatsName,   "1.0.0");
    public static readonly Meter ImportsMeter = new(ImportsName, "1.0.0");
    public static readonly Meter EmailMeter   = new(EmailName,   "1.0.0");

    public static readonly ActivitySource RoundsActivity  = new(RoundsName);
    public static readonly ActivitySource StatsActivity   = new(StatsName);
    public static readonly ActivitySource ImportsActivity = new(ImportsName);

    // ── Rounds ──
    public static readonly Counter<long> RoundsCreated =
        RoundsMeter.CreateCounter<long>("fairwayfinder.rounds.created", description: "Rounds created");
    public static readonly Counter<long> RoundsUpdated =
        RoundsMeter.CreateCounter<long>("fairwayfinder.rounds.updated", description: "Rounds updated");
    public static readonly Counter<long> RoundsDeleted =
        RoundsMeter.CreateCounter<long>("fairwayfinder.rounds.deleted", description: "Rounds deleted");
    public static readonly Counter<long> ShotsLogged =
        RoundsMeter.CreateCounter<long>("fairwayfinder.shots.logged", description: "Individual shots logged");
    public static readonly Histogram<double> RoundSaveDuration =
        RoundsMeter.CreateHistogram<double>("fairwayfinder.round.save.duration", unit: "ms", description: "Time to persist a round create/update");

    // ── Stats ──
    public static readonly Histogram<double> StatsDashDuration =
        StatsMeter.CreateHistogram<double>("fairwayfinder.stats.dashboard.duration", unit: "ms", description: "Time to generate user-stats dashboard");
    public static readonly Histogram<double> StatsCourseDuration =
        StatsMeter.CreateHistogram<double>("fairwayfinder.stats.course.duration", unit: "ms", description: "Time to generate course-stats response");

    // ── Imports ──
    public static readonly Counter<long> GcaImported =
        ImportsMeter.CreateCounter<long>("fairwayfinder.gca.courses.imported", description: "Courses inserted from GolfCourseAPI");
    public static readonly Counter<long> GcaUpdated =
        ImportsMeter.CreateCounter<long>("fairwayfinder.gca.courses.updated", description: "Courses updated from GolfCourseAPI");
    public static readonly Counter<long> GcaSkipped =
        ImportsMeter.CreateCounter<long>("fairwayfinder.gca.courses.skipped", description: "Courses skipped by the GolfCourseAPI importer");
    public static readonly Counter<long> GcaErrors =
        ImportsMeter.CreateCounter<long>("fairwayfinder.gca.import.errors", description: "GolfCourseAPI import errors");
    public static readonly Histogram<double> GcaImportDuration =
        ImportsMeter.CreateHistogram<double>("fairwayfinder.gca.import.duration", unit: "ms", description: "Full GolfCourseAPI import job duration");
    public static readonly Counter<long> TgtrImported =
        ImportsMeter.CreateCounter<long>("fairwayfinder.tgtr.rounds.imported", description: "Rounds imported from TGTR");
    public static readonly Counter<long> TgtrSkipped =
        ImportsMeter.CreateCounter<long>("fairwayfinder.tgtr.rounds.skipped", description: "TGTR rounds already imported");
    public static readonly Counter<long> TgtrErrors =
        ImportsMeter.CreateCounter<long>("fairwayfinder.tgtr.import.errors", description: "TGTR import errors");
    public static readonly Histogram<double> TgtrTransferDuration =
        ImportsMeter.CreateHistogram<double>("fairwayfinder.tgtr.transfer.duration", unit: "ms", description: "TGTR transfer job duration");

    // ── Email ──
    public static readonly Counter<long> EmailSent =
        EmailMeter.CreateCounter<long>("fairwayfinder.email.sent", description: "Emails sent successfully");
    public static readonly Counter<long> EmailFailed =
        EmailMeter.CreateCounter<long>("fairwayfinder.email.failed", description: "Email send failures");
    public static readonly Histogram<double> EmailSendDuration =
        EmailMeter.CreateHistogram<double>("fairwayfinder.email.send.duration", unit: "ms", description: "Email send duration");

    /// <summary>
    /// Activity (span) operation names.
    /// </summary>
    public static class ActivityNames
    {
        public const string RoundCreate = "round.create";
        public const string RoundUpdate = "round.update";
        public const string StatsUserGenerate = "stats.user.generate";
        public const string StatsCourseGenerate = "stats.course.generate";
        public const string ImportsGcaRun = "imports.gca.run";
        public const string ImportsTgtrTransfer = "imports.tgtr.transfer";
    }

    /// <summary>
    /// Metric tag keys. Flat (no dots) by convention — dotted names are for activity tags below.
    /// </summary>
    public static class Tags
    {
        // Rounds
        public const string Holes = "holes";
        public const string ShotTracking = "shot_tracking";
        public const string HoleStats = "hole_stats";
        public const string Operation = "operation";

        // Stats
        public const string HasRounds = "has_rounds";
        public const string HasSg = "has_sg";
        public const string Result = "result";

        // Email
        public const string Kind = "kind";
        public const string Reason = "reason";
    }

    /// <summary>
    /// Activity (span) tag keys. Dotted, OpenTelemetry convention.
    /// </summary>
    public static class ActivityTags
    {
        public const string RoundId = "round.id";
        public const string RoundHoles = "round.holes";
        public const string RoundShotTracking = "round.shot_tracking";

        public const string StatsRoundCount = "stats.round_count";
        public const string StatsFiltered = "stats.filtered";
        public const string StatsCourseId = "stats.course_id";
        public const string StatsTeeboxId = "stats.teebox_id";

        public const string GcaStartPage = "gca.start_page";
        public const string GcaImported = "gca.imported";
        public const string GcaUpdated = "gca.updated";
        public const string GcaSkipped = "gca.skipped";
        public const string GcaErrors = "gca.errors";
        public const string GcaPagesProcessed = "gca.pages_processed";

        public const string TgtrPlayerId = "tgtr.player_id";
        public const string TgtrImported = "tgtr.imported";
        public const string TgtrSkipped = "tgtr.skipped";
        public const string TgtrErrors = "tgtr.errors";
    }

    /// <summary>
    /// Low-cardinality tag values used as enum-like string constants.
    /// </summary>
    public static class TagValues
    {
        public const string OperationCreate = "create";
        public const string OperationUpdate = "update";

        public const string EmailKindConfirmation = "confirmation";
        public const string EmailKindPasswordReset = "password_reset";

        public const string ResultEmpty = "empty";
        public const string ResultFilteredEmpty = "filtered_empty";
        public const string ResultOk = "ok";
        public const string ResultError = "error";
    }
}
