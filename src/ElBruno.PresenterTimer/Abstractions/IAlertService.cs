namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Detects configurable alert conditions from timer state, deduplicates them per section,
/// and broadcasts them via <see cref="AlertRaised"/> (PRD §7.8).
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Raised when a new (non-deduplicated) alert condition is detected.
    /// Raised on the same thread-pool thread as the underlying timer tick or section-change event.
    /// UI consumers must marshal to their dispatcher.
    /// </summary>
    event EventHandler<AlertEventArgs>? AlertRaised;

    /// <summary>
    /// Subscribes to <paramref name="timer"/>'s <c>Tick</c> and <c>SectionChanged</c> events
    /// and begins evaluating alert conditions.  Any previous attachment is released first.
    /// </summary>
    void Attach(ISessionTimerService timer);

    /// <summary>
    /// Unsubscribes from the currently attached timer's events.
    /// Safe to call when nothing is attached.
    /// </summary>
    void Detach();

    /// <summary>
    /// Clears all fired-alert deduplication state so every alert can fire again.
    /// Call on full session reset or when entering a section fresh.
    /// </summary>
    void Reset();
}
