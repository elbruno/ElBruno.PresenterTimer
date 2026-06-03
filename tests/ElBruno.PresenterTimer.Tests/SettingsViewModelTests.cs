using System.Drawing;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.ViewModels;

namespace ElBruno.PresenterTimer.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void Ctor_WhenDeviceNameExists_SelectsMonitorByDeviceName()
    {
        var settings = new AppSettings();
        settings.OverlayLayout.Monitor = 0;
        settings.OverlayLayout.MonitorDeviceName = @"\\.\DISPLAY2";

        var vm = new SettingsViewModel(
            new StubSettingsService(settings),
            new StubFileDialogService(),
            new StubWindowPlacementService());

        Assert.Equal(2, vm.AvailableMonitors.Count);
        Assert.Equal(@"\\.\DISPLAY2", vm.SelectedMonitorDeviceName);
        Assert.Equal(1, vm.Monitor);
    }

    [Fact]
    public void Ctor_WhenDeviceNameMissing_UsesLegacyMonitorIndexFallback()
    {
        var settings = new AppSettings();
        settings.OverlayLayout.Monitor = 1;
        settings.OverlayLayout.MonitorDeviceName = null;

        var vm = new SettingsViewModel(
            new StubSettingsService(settings),
            new StubFileDialogService(),
            new StubWindowPlacementService());

        Assert.Equal(@"\\.\DISPLAY2", vm.SelectedMonitorDeviceName);
        Assert.Equal(1, vm.Monitor);
    }

    [Fact]
    public void Apply_PersistsDeviceNameAndLegacyMonitorIndex()
    {
        var service = new StubSettingsService(new AppSettings());
        var vm = new SettingsViewModel(
            service,
            new StubFileDialogService(),
            new StubWindowPlacementService());

        vm.SelectedMonitorDeviceName = @"\\.\DISPLAY2";
        vm.ApplyCommand.Execute(null);

        Assert.Equal(@"\\.\DISPLAY2", service.Settings.OverlayLayout.MonitorDeviceName);
        Assert.Equal(1, service.Settings.OverlayLayout.Monitor);
    }

    [Fact]
    public void Ctor_LoadsProgressFillOpacity_FromSettings()
    {
        var settings = new AppSettings();
        settings.OverlayStyle.ProgressFillOpacity = 37;

        var vm = new SettingsViewModel(
            new StubSettingsService(settings),
            new StubFileDialogService(),
            new StubWindowPlacementService());

        Assert.Equal(37, vm.ProgressFillOpacity);
    }

    [Fact]
    public void Apply_PersistsProgressFillOpacity()
    {
        var service = new StubSettingsService(new AppSettings());
        var vm = new SettingsViewModel(
            service,
            new StubFileDialogService(),
            new StubWindowPlacementService());

        vm.ProgressFillOpacity = 63;
        vm.ApplyCommand.Execute(null);

        Assert.Equal(63, service.Settings.OverlayStyle.ProgressFillOpacity);
    }

    private sealed class StubSettingsService(AppSettings settings) : ISettingsService
    {
        public AppSettings Settings { get; private set; } = settings;
        public event EventHandler? SettingsApplied;

        public void Load() { }
        public void Save() { }
        public void RaiseSettingsApplied() => SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    private sealed class StubFileDialogService : IFileDialogService
    {
        public string? ShowOpenJsonDialog() => null;
        public string? ShowSaveJsonDialog(string? defaultFileName = null) => null;
        public string? ShowSaveMarkdownDialog(string? defaultFileName = null) => null;
    }

    private sealed class StubWindowPlacementService : IWindowPlacementService
    {
        private readonly IReadOnlyList<MonitorInfo> _monitors =
        [
            new MonitorInfo
            {
                DeviceName = @"\\.\DISPLAY1",
                Bounds = new Rectangle(0, 0, 1920, 1080),
                WorkingArea = new Rectangle(0, 0, 1920, 1040),
                IsPrimary = true
            },
            new MonitorInfo
            {
                DeviceName = @"\\.\DISPLAY2",
                Bounds = new Rectangle(1920, 0, 1920, 1080),
                WorkingArea = new Rectangle(1920, 0, 1920, 1040),
                IsPrimary = false
            }
        ];

        public IReadOnlyList<MonitorInfo> GetAvailableMonitors() => _monitors;
        public MonitorInfo GetPrimaryMonitor() => _monitors[0];
        public MonitorInfo? FindMonitorByDeviceName(string deviceName) =>
            _monitors.FirstOrDefault(m => string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
        public MonitorInfo ResolveMonitor(string? savedDeviceName, int fallbackMonitorIndex = 0) =>
            !string.IsNullOrWhiteSpace(savedDeviceName) && FindMonitorByDeviceName(savedDeviceName) is { } byName
                ? byName
                : _monitors[Math.Clamp(fallbackMonitorIndex, 0, _monitors.Count - 1)];
        public Point ResolvePlacement(OverlayPosition position, MonitorInfo monitor, Size overlaySize, double? customX = null, double? customY = null) => Point.Empty;
        public Point ClampToWorkingArea(Point position, Size overlaySize, MonitorInfo monitor) => position;
        public OverlayPosition ParsePosition(string? positionString) => OverlayPosition.TopCenter;
    }
}
