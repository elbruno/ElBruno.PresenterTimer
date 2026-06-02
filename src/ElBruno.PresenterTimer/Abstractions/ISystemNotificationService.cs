namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Sends Windows toast/notification-centre messages for configurable alert events.
/// Members will be refined in Phase 7 (alerts).
/// </summary>
public interface ISystemNotificationService
{
    /// <summary>Sends a Windows notification with the given title and message.</summary>
    /// <param name="title">Short notification title.</param>
    /// <param name="message">Notification body text.</param>
    void Notify(string title, string message);
}
