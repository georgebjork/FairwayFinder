namespace FairwayFinder.Features.Data;

public class RegisterDeviceRequest
{
    public string DeviceToken { get; set; } = "";
    public string? DeviceName { get; set; }
}

public class SendTestPushRequest
{
    public string TargetUserId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public int? Badge { get; set; }
}

public class SendTestPushResponse
{
    public int Sent { get; set; }
}
