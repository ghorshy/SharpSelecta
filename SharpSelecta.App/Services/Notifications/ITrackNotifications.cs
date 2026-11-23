namespace SharpSelecta.App.Services.Notifications;

public sealed record TrackNotification(string Title, string Body, byte[]? Artwork);

// The desktop's own notifications; each one replaces the previous instead of piling up.
public interface ITrackNotifications
{
    // The Skip button on the latest notification was clicked. Raised on a background thread.
    event EventHandler? SkipRequested;

    Task ShowAsync(TrackNotification notification);
}
