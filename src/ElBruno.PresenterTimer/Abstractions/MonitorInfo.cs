using System.Drawing;

namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Immutable snapshot of a physical monitor's identity and geometry.
/// Populated from <c>System.Windows.Forms.Screen</c> by <see cref="IWindowPlacementService"/>.
/// Using plain <see cref="Rectangle"/> keeps this type WPF-agnostic and unit-testable
/// without a real display (PRD §7.18).
/// </summary>
public sealed class MonitorInfo
{
    /// <summary>
    /// Windows device name, e.g. <c>\\.\DISPLAY1</c>.  Persisted in settings so the
    /// preferred monitor is identified by name rather than index across reboots.
    /// </summary>
    public required string DeviceName { get; init; }

    /// <summary>Total pixel bounds of the monitor (may include taskbar).</summary>
    public required Rectangle Bounds { get; init; }

    /// <summary>Working area of the monitor, excluding taskbar and docked toolbars.</summary>
    public required Rectangle WorkingArea { get; init; }

    /// <summary><see langword="true"/> if this is the system primary monitor.</summary>
    public required bool IsPrimary { get; init; }
}
