namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Named anchor positions for the overlay window, matching the string values stored in
/// <see cref="ElBruno.PresenterTimer.Models.OverlayLayoutSettings.Position"/> (PRD §7.7 / §7.18).
/// </summary>
public enum OverlayPosition
{
    TopCenter,
    TopLeft,
    TopRight,
    BottomCenter,
    BottomLeft,
    BottomRight,
    /// <summary>Use <c>CustomX</c> / <c>CustomY</c> from settings instead of a computed anchor.</summary>
    Custom
}
