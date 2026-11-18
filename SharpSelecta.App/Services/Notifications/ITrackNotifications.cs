namespace SharpSelecta.App.Services.Notifications;

public sealed record TrackNotification(string Title, string Body, byte[]? Artwork);

// The desktop's own notifications; each one replaces the previous instead of piling up.
public interface ITrackNotifications
{
    Task ShowAsync(TrackNotification notification);
}
