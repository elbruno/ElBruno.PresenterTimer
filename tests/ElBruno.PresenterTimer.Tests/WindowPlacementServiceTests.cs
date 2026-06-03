using System.Drawing;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.Tests;

/// <summary>
/// Unit tests for <see cref="WindowPlacementService"/>.
/// An injected monitor-provider avoids any dependency on real hardware.
/// </summary>
public class WindowPlacementServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static MonitorInfo MakeMonitor(
        string deviceName,
        Rectangle bounds,
        bool isPrimary = false)
        => new()
        {
            DeviceName  = deviceName,
            Bounds      = bounds,
            WorkingArea = bounds, // simplify tests by making working area == bounds
            IsPrimary   = isPrimary
        };

    private static MonitorInfo MakeMonitorWithTaskbar(
        string deviceName,
        Rectangle bounds,
        int taskbarHeight = 40,
        bool isPrimary = false)
        => new()
        {
            DeviceName  = deviceName,
            Bounds      = bounds,
            WorkingArea = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height - taskbarHeight),
            IsPrimary   = isPrimary
        };

    private static WindowPlacementService MakeService(params MonitorInfo[] monitors)
    {
        var list = (IReadOnlyList<MonitorInfo>)monitors.ToList().AsReadOnly();
        return new WindowPlacementService(() => list);
    }

    // Two monitors used across multiple tests
    private static readonly MonitorInfo Primary   = MakeMonitor(@"\\.\DISPLAY1", new Rectangle(0, 0, 1920, 1080), isPrimary: true);
    private static readonly MonitorInfo Secondary = MakeMonitor(@"\\.\DISPLAY2", new Rectangle(1920, 0, 2560, 1440));

    // ── GetAvailableMonitors ─────────────────────────────────────────────────

    [Fact]
    public void GetAvailableMonitors_ReturnsInjectedList()
    {
        var svc = MakeService(Primary, Secondary);

        var monitors = svc.GetAvailableMonitors();

        Assert.Equal(2, monitors.Count);
    }

    // ── GetPrimaryMonitor ────────────────────────────────────────────────────

    [Fact]
    public void GetPrimaryMonitor_ReturnsFlaggedPrimary()
    {
        var svc = MakeService(Secondary, Primary); // Primary is not at index 0

        Assert.Equal(@"\\.\DISPLAY1", svc.GetPrimaryMonitor().DeviceName);
    }

    [Fact]
    public void GetPrimaryMonitor_NoPrimaryFlag_ReturnsFirst()
    {
        var a = MakeMonitor(@"\\.\DISPLAY1", new Rectangle(0, 0, 1920, 1080), isPrimary: false);
        var b = MakeMonitor(@"\\.\DISPLAY2", new Rectangle(1920, 0, 1920, 1080), isPrimary: false);
        var svc = MakeService(a, b);

        Assert.Equal(@"\\.\DISPLAY1", svc.GetPrimaryMonitor().DeviceName);
    }

    // ── FindMonitorByDeviceName ──────────────────────────────────────────────

    [Fact]
    public void FindMonitorByDeviceName_KnownName_ReturnsMonitor()
    {
        var svc = MakeService(Primary, Secondary);

        var found = svc.FindMonitorByDeviceName(@"\\.\DISPLAY2");

        Assert.NotNull(found);
        Assert.Equal(@"\\.\DISPLAY2", found!.DeviceName);
    }

    [Fact]
    public void FindMonitorByDeviceName_UnknownName_ReturnsNull()
    {
        var svc = MakeService(Primary);

        Assert.Null(svc.FindMonitorByDeviceName(@"\\.\DISPLAY99"));
    }

    [Fact]
    public void FindMonitorByDeviceName_CaseInsensitive()
    {
        var svc = MakeService(Primary);

        Assert.NotNull(svc.FindMonitorByDeviceName(@"\\.\display1"));
    }

    [Fact]
    public void FindMonitorByDeviceName_NullOrEmpty_ReturnsNull()
    {
        var svc = MakeService(Primary);

        Assert.Null(svc.FindMonitorByDeviceName(null!));
        Assert.Null(svc.FindMonitorByDeviceName(""));
        Assert.Null(svc.FindMonitorByDeviceName("   "));
    }

    // ── ResolveMonitor ───────────────────────────────────────────────────────

    [Fact]
    public void ResolveMonitor_KnownDeviceName_ReturnsNamedMonitor()
    {
        var svc = MakeService(Primary, Secondary);

        var monitor = svc.ResolveMonitor(@"\\.\DISPLAY2");

        Assert.Equal(@"\\.\DISPLAY2", monitor.DeviceName);
    }

    [Fact]
    public void ResolveMonitor_DisconnectedDevice_FallsBackToPrimary()
    {
        // Secondary is not in the list → simulates disconnected monitor (PRD §7.18)
        var svc = MakeService(Primary);

        var monitor = svc.ResolveMonitor(@"\\.\DISPLAY2");

        Assert.Equal(@"\\.\DISPLAY1", monitor.DeviceName);
        Assert.True(monitor.IsPrimary);
    }

    [Fact]
    public void ResolveMonitor_NullDeviceName_FallsBackToPrimary()
    {
        var svc = MakeService(Primary, Secondary);

        var monitor = svc.ResolveMonitor(null);

        Assert.True(monitor.IsPrimary);
    }

    [Fact]
    public void ResolveMonitor_NullDeviceName_UsesFallbackMonitorIndex()
    {
        var svc = MakeService(Primary, Secondary);

        var monitor = svc.ResolveMonitor(null, fallbackMonitorIndex: 1);

        Assert.Equal(@"\\.\DISPLAY2", monitor.DeviceName);
    }

    [Fact]
    public void ResolveMonitor_DisconnectedDevice_UsesFallbackMonitorIndex()
    {
        var svc = MakeService(Primary, Secondary);

        var monitor = svc.ResolveMonitor(@"\\.\DISPLAY99", fallbackMonitorIndex: 1);

        Assert.Equal(@"\\.\DISPLAY2", monitor.DeviceName);
    }

    [Fact]
    public void ResolveMonitor_InvalidFallbackIndex_FallsBackToPrimary()
    {
        var svc = MakeService(Secondary, Primary);

        var monitor = svc.ResolveMonitor(null, fallbackMonitorIndex: 99);

        Assert.Equal(@"\\.\DISPLAY1", monitor.DeviceName);
        Assert.True(monitor.IsPrimary);
    }

    // ── ResolvePlacement ─────────────────────────────────────────────────────

    // Working area: Rectangle(0, 0, 1920, 1040)  with 40 px taskbar
    private static readonly MonitorInfo TestMonitor =
        MakeMonitorWithTaskbar(@"\\.\DISPLAY1", new Rectangle(0, 0, 1920, 1080), taskbarHeight: 40, isPrimary: true);

    private static readonly Size OverlaySize = new(960, 80);

    [Fact]
    public void ResolvePlacement_TopCenter_CenteredAtTop()
    {
        var svc = MakeService(TestMonitor);
        var pt = svc.ResolvePlacement(OverlayPosition.TopCenter, TestMonitor, OverlaySize);

        // X = (1920 - 960) / 2 = 480; Y = 0 (top of working area)
        Assert.Equal(480, pt.X);
        Assert.Equal(0, pt.Y);
    }

    [Fact]
    public void ResolvePlacement_TopLeft_AtTopLeft()
    {
        var svc = MakeService(TestMonitor);
        var pt = svc.ResolvePlacement(OverlayPosition.TopLeft, TestMonitor, OverlaySize);

        Assert.Equal(0, pt.X);
        Assert.Equal(0, pt.Y);
    }

    [Fact]
    public void ResolvePlacement_TopRight_AtTopRight()
    {
        var svc = MakeService(TestMonitor);
        var pt = svc.ResolvePlacement(OverlayPosition.TopRight, TestMonitor, OverlaySize);

        // X = 1920 - 960 = 960; Y = 0
        Assert.Equal(960, pt.X);
        Assert.Equal(0, pt.Y);
    }

    [Fact]
    public void ResolvePlacement_BottomCenter_CenteredAtBottom()
    {
        var svc = MakeService(TestMonitor);
        var pt = svc.ResolvePlacement(OverlayPosition.BottomCenter, TestMonitor, OverlaySize);

        // X = (1920 - 960) / 2 = 480; Y = 1040 - 80 = 960
        Assert.Equal(480, pt.X);
        Assert.Equal(960, pt.Y);
    }

    [Fact]
    public void ResolvePlacement_BottomLeft_AtBottomLeft()
    {
        var svc = MakeService(TestMonitor);
        var pt = svc.ResolvePlacement(OverlayPosition.BottomLeft, TestMonitor, OverlaySize);

        Assert.Equal(0, pt.X);
        Assert.Equal(960, pt.Y);
    }

    [Fact]
    public void ResolvePlacement_BottomRight_AtBottomRight()
    {
        var svc = MakeService(TestMonitor);
        var pt = svc.ResolvePlacement(OverlayPosition.BottomRight, TestMonitor, OverlaySize);

        Assert.Equal(960, pt.X);
        Assert.Equal(960, pt.Y);
    }

    [Fact]
    public void ResolvePlacement_Custom_UsesProvidedCoords()
    {
        var svc = MakeService(TestMonitor);
        var pt = svc.ResolvePlacement(OverlayPosition.Custom, TestMonitor, OverlaySize, customX: 100.5, customY: 200.7);

        Assert.Equal(100, pt.X);
        Assert.Equal(200, pt.Y);
    }

    [Fact]
    public void ResolvePlacement_CustomNoCoords_FallsBackToTopCenter()
    {
        var svc = MakeService(TestMonitor);
        var pt = svc.ResolvePlacement(OverlayPosition.Custom, TestMonitor, OverlaySize);

        // Should behave like TopCenter
        var topCenter = svc.ResolvePlacement(OverlayPosition.TopCenter, TestMonitor, OverlaySize);
        Assert.Equal(topCenter, pt);
    }

    [Fact]
    public void ResolvePlacement_SecondaryMonitor_UsesSecondaryWorkingArea()
    {
        // Secondary at X=1920, working area same as bounds for simplicity
        var secondary = MakeMonitor(@"\\.\DISPLAY2", new Rectangle(1920, 0, 2560, 1440));
        var svc = MakeService(Primary, secondary);

        var pt = svc.ResolvePlacement(OverlayPosition.TopCenter, secondary, OverlaySize);

        // X = 1920 + (2560 - 960) / 2 = 1920 + 800 = 2720; Y = 0
        Assert.Equal(2720, pt.X);
        Assert.Equal(0, pt.Y);
    }

    // ── ClampToWorkingArea ───────────────────────────────────────────────────

    [Fact]
    public void ClampToWorkingArea_InsideBounds_Unchanged()
    {
        var svc = MakeService(TestMonitor);
        var pt = new Point(200, 100);

        var clamped = svc.ClampToWorkingArea(pt, OverlaySize, TestMonitor);

        Assert.Equal(pt, clamped);
    }

    [Fact]
    public void ClampToWorkingArea_TooFarRight_ClampsToRightEdge()
    {
        var svc = MakeService(TestMonitor);
        // Position such that right edge would be at 1920+500 = beyond working area
        var pt = new Point(1500, 0);

        var clamped = svc.ClampToWorkingArea(pt, OverlaySize, TestMonitor);

        // Max X = 1920 - 960 = 960
        Assert.Equal(960, clamped.X);
        Assert.Equal(0, clamped.Y);
    }

    [Fact]
    public void ClampToWorkingArea_NegativeX_ClampsToLeftEdge()
    {
        var svc = MakeService(TestMonitor);
        var pt = new Point(-50, 0);

        var clamped = svc.ClampToWorkingArea(pt, OverlaySize, TestMonitor);

        Assert.Equal(0, clamped.X);
    }

    [Fact]
    public void ClampToWorkingArea_TooFarDown_ClampsToBottomEdge()
    {
        var svc = MakeService(TestMonitor);
        var pt = new Point(0, 1100); // beyond working area (1040)

        var clamped = svc.ClampToWorkingArea(pt, OverlaySize, TestMonitor);

        // Max Y = 1040 - 80 = 960
        Assert.Equal(960, clamped.Y);
    }

    [Fact]
    public void ClampToWorkingArea_NegativeY_ClampsToTopEdge()
    {
        var svc = MakeService(TestMonitor);
        var pt = new Point(0, -100);

        var clamped = svc.ClampToWorkingArea(pt, OverlaySize, TestMonitor);

        Assert.Equal(0, clamped.Y);
    }

    // ── ParsePosition ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("TopCenter",    OverlayPosition.TopCenter)]
    [InlineData("TopLeft",      OverlayPosition.TopLeft)]
    [InlineData("TopRight",     OverlayPosition.TopRight)]
    [InlineData("BottomCenter", OverlayPosition.BottomCenter)]
    [InlineData("BottomLeft",   OverlayPosition.BottomLeft)]
    [InlineData("BottomRight",  OverlayPosition.BottomRight)]
    [InlineData("Custom",       OverlayPosition.Custom)]
    public void ParsePosition_KnownStrings_ParseCorrectly(string input, OverlayPosition expected)
    {
        var svc = MakeService(Primary);

        Assert.Equal(expected, svc.ParsePosition(input));
    }

    [Theory]
    [InlineData("topcenter")]   // lowercase
    [InlineData("TOPCENTER")]   // uppercase
    public void ParsePosition_CaseInsensitive(string input)
    {
        var svc = MakeService(Primary);

        Assert.Equal(OverlayPosition.TopCenter, svc.ParsePosition(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("UnknownValue")]
    public void ParsePosition_InvalidOrEmpty_ReturnsTopCenter(string? input)
    {
        var svc = MakeService(Primary);

        Assert.Equal(OverlayPosition.TopCenter, svc.ParsePosition(input));
    }
}
