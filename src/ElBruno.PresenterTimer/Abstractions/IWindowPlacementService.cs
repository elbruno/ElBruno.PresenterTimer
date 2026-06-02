using System.Drawing;

namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Monitor enumeration and overlay placement helpers for the timeline overlay (PRD §7.7 / §7.18).
/// Returns plain <see cref="Rectangle"/> / <see cref="Point"/> structs so callers remain
/// WPF-agnostic and the service is unit-testable without a real display.
/// </summary>
public interface IWindowPlacementService
{
    /// <summary>
    /// Returns a snapshot of all currently connected monitors.  The list is re-read on each
    /// call so callers always see the live configuration.
    /// </summary>
    IReadOnlyList<MonitorInfo> GetAvailableMonitors();

    /// <summary>Returns the system primary monitor.</summary>
    MonitorInfo GetPrimaryMonitor();

    /// <summary>
    /// Finds a monitor by its Windows device name (e.g. <c>\\.\DISPLAY1</c>).
    /// Returns <see langword="null"/> if the device is not currently connected.
    /// </summary>
    MonitorInfo? FindMonitorByDeviceName(string deviceName);

    /// <summary>
    /// Resolves the monitor to use for the overlay:
    /// <list type="bullet">
    ///   <item>If <paramref name="savedDeviceName"/> is non-null and the monitor is connected, that monitor is returned.</item>
    ///   <item>Otherwise, falls back to the primary monitor (PRD §7.18).</item>
    /// </list>
    /// Never throws.
    /// </summary>
    MonitorInfo ResolveMonitor(string? savedDeviceName);

    /// <summary>
    /// Computes the top-left pixel coordinate for an overlay of size <paramref name="overlaySize"/>
    /// anchored at <paramref name="position"/> within <paramref name="monitor"/>'s working area.
    /// For <see cref="OverlayPosition.Custom"/> the caller must supply the saved
    /// <paramref name="customX"/> / <paramref name="customY"/> values; if those are <see langword="null"/>
    /// the method falls back to <see cref="OverlayPosition.TopCenter"/>.
    /// </summary>
    Point ResolvePlacement(
        OverlayPosition position,
        MonitorInfo monitor,
        Size overlaySize,
        double? customX = null,
        double? customY = null);

    /// <summary>
    /// Clamps <paramref name="position"/> so that a window of <paramref name="overlaySize"/>
    /// stays fully within <paramref name="monitor"/>'s working area.
    /// Useful after dragging or when the monitor shrinks (PRD §10.3).
    /// </summary>
    Point ClampToWorkingArea(Point position, Size overlaySize, MonitorInfo monitor);

    /// <summary>
    /// Parses a position string from settings (e.g. "TopCenter") into the typed
    /// <see cref="OverlayPosition"/> enum.  Returns <see cref="OverlayPosition.TopCenter"/>
    /// for unknown values rather than throwing.
    /// </summary>
    OverlayPosition ParsePosition(string? positionString);
}
