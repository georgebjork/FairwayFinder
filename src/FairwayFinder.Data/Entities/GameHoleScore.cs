namespace FairwayFinder.Data.Entities;

/// <summary>
/// A stroke count the host entered for a participant who has no round of their own.
///
/// Deliberately not a shadow <see cref="Round"/>: <c>ix_round_user_id_active</c> is keyed on
/// <c>user_id</c>, so a guest would need a synthetic one, and those rounds would leak into round
/// lists and stats. A four-column table avoids all of it.
///
/// Keys on <see cref="HoleNumber"/> rather than a hole id (as <see cref="Score"/> does) because a
/// guest has a teebox but no round to resolve a hole against.
/// </summary>
public class GameHoleScore
{
    public long GameHoleScoreId { get; set; }

    public long GameParticipantId { get; set; }

    public int HoleNumber { get; set; }

    public short Strokes { get; set; }

    public string CreatedBy { get; set; } = null!;

    public DateOnly CreatedOn { get; set; }

    public string UpdatedBy { get; set; } = null!;

    public DateOnly UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation properties
    public virtual GameParticipant GameParticipant { get; set; } = null!;
}
