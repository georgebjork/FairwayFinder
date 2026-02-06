namespace FairwayFinder.Web.Services;

public interface INotificationService
{
    event Action? OnAlertMessage;
    event Action? OnToastMessage;
    
    void SendAlertMessage(string message, NotificationType type = NotificationType.Success);
    void SendToastMessage(string message, NotificationType type = NotificationType.Success, string? summary = null);

    bool TryGetAlertMessage(out Notification notification);
    bool TryGetToastMessage(out Notification notification);
}

public class NotificationService : INotificationService
{
    public event Action? OnAlertMessage;
    public event Action? OnToastMessage;

    private Notification? _alertMessage;
    private Notification? _toastMessage;
    
    public void SendAlertMessage(string message, NotificationType type = NotificationType.Success)
    {
        _alertMessage = new Notification(message, type);
        
        OnAlertMessage?.Invoke();
        _alertMessage = null;
    }

    public void SendToastMessage(string message, NotificationType type = NotificationType.Success, string? summary = null)
    {
        _toastMessage = new Notification(message, type, summary);
        
        OnToastMessage?.Invoke();
        _toastMessage = null;
    }

    public bool TryGetAlertMessage(out Notification notification)
    {
        if (_alertMessage.HasValue)
        {
            notification = _alertMessage.Value;
            return true;
        }
        
        notification = default;
        return false;
    }

    public bool TryGetToastMessage(out Notification notification)
    {
        if (_toastMessage.HasValue)
        {
            notification = _toastMessage.Value;
            return true;
        }
        
        notification = default;
        return false;
    }
}

public readonly struct Notification
{
    public Notification(string message, NotificationType type, string? summary = null)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Type = type;
        Id = Guid.NewGuid();
        Timestamp = DateTime.UtcNow;
        Summary = summary;
    }
    
    public Guid Id { get; }
    public string Message { get; }
    public string? Summary { get; }
    public NotificationType Type { get; }
    public DateTime Timestamp { get; }
}

public enum NotificationType
{
    Info,
    Warning, 
    Error,
    Success
}