using System.Drawing;
using System.Windows.Forms;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Shows Windows balloon-tip notifications for configurable alert events (PRD §7.8, §10.2).
///
/// <para><b>Non-focus-stealing</b> — balloon tips are displayed in the notification area and
/// never steal keyboard focus, satisfying PRD §10.2.</para>
///
/// <para><b>Default OFF</b> — <see cref="Notify"/> is a no-op when
/// <c>AlertSettings.EnableWindowsNotifications</c> is <c>false</c> (PRD §7.8).</para>
///
/// <para><b>Tray icon usage:</b> a <see cref="NotifyIcon"/> is required to display balloon tips.
/// The preferred approach is to pass the application's existing tray icon via the constructor
/// (<paramref name="sharedIcon"/> overload) — this avoids creating a second icon in the
/// notification area. If no shared icon is provided, this service creates and manages its own
/// <see cref="NotifyIcon"/> internally (it is disposed in <see cref="Dispose"/>). The
/// App layer should call the <c>(AlertSettings, NotifyIcon)</c> constructor and pass the icon
/// that <c>TrayIconService</c> already owns, so Dallas/Kane can wire this up without a
/// second tray entry.</para>
///
/// <para><b>Thread safety:</b> <see cref="Notify"/> marshals to the WinForms message pump if
/// needed; call from any thread.</para>
/// </summary>
public sealed class SystemNotificationService : ISystemNotificationService, IDisposable
{
    private const int BalloonTipTimeoutMs = 5000;

    private readonly AlertSettings _settings;
    private readonly NotifyIcon? _sharedIcon;
    private NotifyIcon? _ownedIcon;
    private bool _disposed;

    // ── Constructors ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a self-contained notification service that manages its own tray icon.
    /// </summary>
    /// <param name="settings">Shared alert settings. Reads <c>EnableWindowsNotifications</c> on every call.</param>
    public SystemNotificationService(AlertSettings settings)
        : this(settings, sharedIcon: null) { }

    /// <summary>
    /// Creates a notification service that uses <paramref name="sharedIcon"/> for balloon tips.
    /// Prefer this overload — pass the icon already owned by <c>TrayIconService</c> to avoid
    /// a second notification-area entry.
    /// </summary>
    /// <param name="settings">Shared alert settings.</param>
    /// <param name="sharedIcon">
    /// The application's existing <see cref="NotifyIcon"/>, or <c>null</c> to let this
    /// service create and own a private icon.
    /// </param>
    public SystemNotificationService(AlertSettings settings, NotifyIcon? sharedIcon)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _sharedIcon = sharedIcon;
    }

    // ── ISystemNotificationService ────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// No-op when <c>AlertSettings.EnableWindowsNotifications</c> is <c>false</c>.
    /// Safe to call from a background thread; the balloon is shown on the UI thread
    /// via WinForms internal dispatch.
    /// </remarks>
    public void Notify(string title, string message)
    {
        if (_disposed) return;
        if (!_settings.EnableWindowsNotifications) return;

        var icon = GetOrCreateIcon();
        icon.ShowBalloonTip(BalloonTipTimeoutMs, title, message, ToolTipIcon.Info);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Only dispose what we own — never dispose the shared icon (App owns its lifecycle).
        _ownedIcon?.Dispose();
        _ownedIcon = null;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private NotifyIcon GetOrCreateIcon()
    {
        if (_sharedIcon is not null) return _sharedIcon;

        // Lazy-create a self-managed icon on first use.
        if (_ownedIcon is null)
        {
            _ownedIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Session Timeline Overlay",
                Visible = true
            };
        }

        return _ownedIcon;
    }
}
