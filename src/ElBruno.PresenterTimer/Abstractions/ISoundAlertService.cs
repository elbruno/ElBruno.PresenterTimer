namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Plays non-blocking system sounds for configurable alert events (PRD §7.8).
/// All methods are fire-and-forget; they return immediately and do nothing when
/// <see cref="IsEnabled"/> is <c>false</c>.
/// <para><b>Default OFF</b> — <see cref="IsEnabled"/> mirrors
/// <c>AlertSettings.EnableSoundAlerts</c> which defaults to <c>false</c> (PRD §7.8).</para>
/// </summary>
public interface ISoundAlertService
{
    /// <summary>
    /// Gets or sets whether sound alerts are active.
    /// Delegates to <c>AlertSettings.EnableSoundAlerts</c>; changes take effect immediately.
    /// Default: <c>false</c> (PRD §7.8).
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Plays the section-warning sound (e.g., X minutes left in a section).
    /// No-op when <see cref="IsEnabled"/> is <c>false</c>.
    /// </summary>
    void PlaySectionWarning();

    /// <summary>
    /// Plays the section-end sound when a section reaches its planned duration.
    /// No-op when <see cref="IsEnabled"/> is <c>false</c>.
    /// </summary>
    void PlaySectionEnd();

    /// <summary>
    /// Plays the session-end sound when the full session planned duration is reached.
    /// No-op when <see cref="IsEnabled"/> is <c>false</c>.
    /// </summary>
    void PlaySessionEnd();

    /// <summary>
    /// Plays a test sound unconditionally (ignores <see cref="IsEnabled"/>).
    /// Called by the Settings UI "Test sound" button (PRD §7.8).
    /// </summary>
    void PlayTestSound();
}
