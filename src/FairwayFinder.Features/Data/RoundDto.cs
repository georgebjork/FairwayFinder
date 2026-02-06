namespace FairwayFinder.Features.Data;

public class RoundDto
{
    public long RoundId { get; set; }
    public DateOnly DatePlayed { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public int Score { get; set; }
}
