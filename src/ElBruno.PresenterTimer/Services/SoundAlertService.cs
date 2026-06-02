using System.Media;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Plays Windows system sounds for timer alert events (PRD §7.8).
///
/// <para><b>Non-blocking</b> — <see cref="SystemSound.Play"/> dispatches audio through the OS
/// audio subsystem and returns immediately; no extra threads are created by this service.</para>
///
/// <para><b>Enabled flag</b> — all <c>Play*</c> methods (except <see cref="PlayTestSound"/>)
/// are no-ops when <see cref="IsEnabled"/> is <c>false</c>.
/// <see cref="IsEnabled"/> reads/writes <c>AlertSettings.EnableSoundAlerts</c> directly so
/// changes from the Settings UI are picked up without restarting the service.</para>
///
/// <para><b>Sound mapping</b> (maps to the user's configured Windows system sounds):</para>
/// <list type="bullet">
///   <item><see cref="PlaySectionWarning"/> → <see cref="SystemSounds.Asterisk"/> (low-priority alert)</item>
///   <item><see cref="PlaySectionEnd"/>     → <see cref="SystemSounds.Exclamation"/> (standard alert)</item>
///   <item><see cref="PlaySessionEnd"/>     → <see cref="SystemSounds.Exclamation"/> (standard alert)</item>
///   <item><see cref="PlayTestSound"/>      → <see cref="SystemSounds.Beep"/> (always fires)</item>
/// </list>
/// </summary>
public sealed class SoundAlertService : ISoundAlertService
{
    private readonly AlertSettings _settings;

    /// <param name="settings">
    /// Shared alert settings. <see cref="IsEnabled"/> reads and writes
    /// <c>settings.EnableSoundAlerts</c> directly, so UI changes propagate instantly.
    /// </param>
    public SoundAlertService(AlertSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc/>
    public bool IsEnabled
    {
        get => _settings.EnableSoundAlerts;
        set => _settings.EnableSoundAlerts = value;
    }

    /// <inheritdoc/>
    public void PlaySectionWarning()
    {
        if (!_settings.EnableSoundAlerts) return;
        SystemSounds.Asterisk.Play();
    }

    /// <inheritdoc/>
    public void PlaySectionEnd()
    {
        if (!_settings.EnableSoundAlerts) return;
        SystemSounds.Exclamation.Play();
    }

    /// <inheritdoc/>
    public void PlaySessionEnd()
    {
        if (!_settings.EnableSoundAlerts) return;
        SystemSounds.Exclamation.Play();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Plays regardless of <see cref="IsEnabled"/> so the Settings UI test-sound button
    /// always produces audible feedback (PRD §7.8 "Test sound button").
    /// </remarks>
    public void PlayTestSound() => SystemSounds.Beep.Play();
}
