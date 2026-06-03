using System.Drawing;
using System.Windows.Forms;
using ElBruno.PresenterTimer.Abstractions;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Monitor enumeration and overlay placement helpers for the timeline overlay (PRD §7.7 / §7.18).
/// Uses <see cref="Screen"/> for live monitor data; an injectable <paramref name="monitorProvider"/>
/// allows unit tests to pass synthetic monitor lists without real displays.
/// </summary>
public sealed class WindowPlacementService : IWindowPlacementService
{
    private readonly Func<IReadOnlyList<MonitorInfo>> _monitorProvider;

    /// <param name="monitorProvider">
    /// Optional override that supplies the monitor list.  When <see langword="null"/> the
    /// service uses <see cref="Screen.AllScreens"/> (production default).
    /// </param>
    public WindowPlacementService(Func<IReadOnlyList<MonitorInfo>>? monitorProvider = null)
    {
        _monitorProvider = monitorProvider ?? ReadFromScreens;
    }

    // ── IWindowPlacementService ──────────────────────────────────────────────

    /// <inheritdoc/>
    public IReadOnlyList<MonitorInfo> GetAvailableMonitors() => _monitorProvider();

    /// <inheritdoc/>
    public MonitorInfo GetPrimaryMonitor()
    {
        var monitors = _monitorProvider();
        return monitors.FirstOrDefault(m => m.IsPrimary)
               ?? monitors.FirstOrDefault()
               ?? FallbackMonitor();
    }

    /// <inheritdoc/>
    public MonitorInfo? FindMonitorByDeviceName(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return null;

        return _monitorProvider()
            .FirstOrDefault(m => string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public MonitorInfo ResolveMonitor(string? savedDeviceName, int fallbackMonitorIndex = 0)
    {
        // Try to find the saved device first.
        if (!string.IsNullOrWhiteSpace(savedDeviceName))
        {
            var found = FindMonitorByDeviceName(savedDeviceName);
            if (found is not null)
                return found;
        }

        // Backward compatibility: use legacy monitor index if available.
        var monitors = _monitorProvider();
        if (fallbackMonitorIndex >= 0 && fallbackMonitorIndex < monitors.Count)
            return monitors[fallbackMonitorIndex];

        // Last resort: primary monitor.
        return GetPrimaryMonitor();
    }

    /// <inheritdoc/>
    public Point ResolvePlacement(
        OverlayPosition position,
        MonitorInfo monitor,
        Size overlaySize,
        double? customX = null,
        double? customY = null)
    {
        var wa = monitor.WorkingArea;

        return position switch
        {
            OverlayPosition.TopLeft     => new Point(wa.Left, wa.Top),
            OverlayPosition.TopRight    => new Point(wa.Right - overlaySize.Width, wa.Top),
            OverlayPosition.TopCenter   => new Point(wa.Left + (wa.Width - overlaySize.Width) / 2, wa.Top),
            OverlayPosition.BottomLeft  => new Point(wa.Left, wa.Bottom - overlaySize.Height),
            OverlayPosition.BottomRight => new Point(wa.Right - overlaySize.Width, wa.Bottom - overlaySize.Height),
            OverlayPosition.BottomCenter => new Point(wa.Left + (wa.Width - overlaySize.Width) / 2, wa.Bottom - overlaySize.Height),
            OverlayPosition.Custom when customX.HasValue && customY.HasValue
                => new Point((int)customX.Value, (int)customY.Value),

            // Custom with no saved values → fall back to TopCenter
            _ => new Point(wa.Left + (wa.Width - overlaySize.Width) / 2, wa.Top),
        };
    }

    /// <inheritdoc/>
    public Point ClampToWorkingArea(Point position, Size overlaySize, MonitorInfo monitor)
    {
        var wa = monitor.WorkingArea;

        int x = Math.Max(wa.Left, Math.Min(position.X, wa.Right - overlaySize.Width));
        int y = Math.Max(wa.Top, Math.Min(position.Y, wa.Bottom - overlaySize.Height));

        return new Point(x, y);
    }

    /// <inheritdoc/>
    public OverlayPosition ParsePosition(string? positionString)
    {
        if (string.IsNullOrWhiteSpace(positionString))
            return OverlayPosition.TopCenter;

        return Enum.TryParse<OverlayPosition>(positionString, ignoreCase: true, out var result)
            ? result
            : OverlayPosition.TopCenter;
    }

    // ── private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Reads the live monitor list from <see cref="Screen.AllScreens"/>.
    /// This is the production implementation.
    /// </summary>
    private static IReadOnlyList<MonitorInfo> ReadFromScreens()
    {
        return Screen.AllScreens
            .Select(s => new MonitorInfo
            {
                DeviceName  = s.DeviceName,
                Bounds      = s.Bounds,
                WorkingArea = s.WorkingArea,
                IsPrimary   = s.Primary
            })
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Emergency fallback when no monitors are reported (should not happen in practice but
    /// avoids NullReferenceException per PRD §10.3).
    /// </summary>
    private static MonitorInfo FallbackMonitor() => new()
    {
        DeviceName  = @"\\.\DISPLAY1",
        Bounds      = new Rectangle(0, 0, 1920, 1080),
        WorkingArea = new Rectangle(0, 0, 1920, 1040),
        IsPrimary   = true
    };
}
