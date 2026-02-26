namespace FairwayFinder.Features.Data.TGTR;

public class TgtrTransferResult
{
    public int RoundsImported { get; set; }
    public int RoundsSkipped { get; set; }
    public List<TgtrTransferError> Errors { get; set; } = new();
}

public class TgtrTransferError
{
    public int TgtrRoundId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
