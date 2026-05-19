namespace FairwayFinder.Shared.Settings;

public class ApnsSettings
{
    public string BundleId { get; set; } = "";
    public string KeyId { get; set; } = "";
    public string TeamId { get; set; } = "";
    public string P8Contents { get; set; } = "";
    public bool UseSandbox { get; set; }
}
