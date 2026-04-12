using FairwayFinder.Features.Data;

namespace FairwayFinder.Web.Components.Pages.Rounds.Components;

/// <summary>
/// Shared mutable state for the Create Round wizard.
/// Owned by CreateRound.razor, passed by reference to step components.
/// </summary>
public class CreateRoundState
{
    // Step 1: Setup
    public List<CourseSearchResult> CourseSearchResults { get; set; } = [];
    public long? SelectedCourseId { get; set; }
    public string SelectedCourseName { get; set; } = string.Empty;
    public List<TeeboxOption> TeeboxOptions { get; set; } = [];
    public bool LoadingTeeboxes { get; set; }
    public long? SelectedTeeboxId { get; set; }
    public string SelectedTeeboxName { get; set; } = string.Empty;
    public string SelectedRoundType { get; set; } = "18";
    public string SelectedNine { get; set; } = "front";
    public DateOnly DatePlayed { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public bool TrackAdvancedStats { get; set; }
    public bool TrackShotByShotStats { get; set; }

    /// <summary>
    /// Tracking mode: "scorecard", "advanced", or "shotbyshot"
    /// </summary>
    public string TrackingMode
    {
        get
        {
            if (TrackShotByShotStats) return "shotbyshot";
            if (TrackAdvancedStats) return "advanced";
            return "scorecard";
        }
        set
        {
            TrackShotByShotStats = value == "shotbyshot";
            TrackAdvancedStats = value == "advanced" || value == "shotbyshot";
        }
    }

    // Step 2: Scorecard
    public List<HoleInfo> Holes { get; set; } = [];
    public List<HoleScoreEntry> HoleEntries { get; set; } = [];

    // Step 3: Review
    public RoundResponse? PreviewRound { get; set; }

    public bool IsSetupValid => SelectedCourseId.HasValue && SelectedTeeboxId.HasValue;
}
