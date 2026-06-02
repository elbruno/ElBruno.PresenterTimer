# Ripley — History

## Seed
- Project: ElBruno.PresenterTimer — .NET 10 WPF systray "Session Timeline Overlay" app.
- PRD: docs/SessionTimelineOverlay_PRD.md (Full MVP scope, 9 phases).
- Namespace/solution: ElBruno.PresenterTimer.
- Requested by: elbruno.

## Learnings

### Phase 0 — Solution Scaffold (2026-06-02)

#### Project structure (as built)

```
ElBruno.PresenterTimer.sln                      ← classic .sln (use -f sln flag; .NET 10 defaults to .slnx)
src/
  ElBruno.PresenterTimer/
    ElBruno.PresenterTimer.csproj               ← net10.0-windows, UseWPF, WinExe
    App.xaml / App.xaml.cs                      ← default WPF entry; StartupUri=MainWindow.xaml (Phase 1 will remove it)
    MainWindow.xaml / MainWindow.xaml.cs        ← template-generated; Phase 1 will replace with tray-only
    ViewModels/
      ViewModelBase.cs                          ← INotifyPropertyChanged + SetProperty<T>
      RelayCommand.cs                           ← ICommand, delegates to Action/Func
    Abstractions/
      IAlertService.cs
      IFileDialogService.cs
      ISessionTimerService.cs
      ISettingsService.cs
      ISystemNotificationService.cs
      ITrayIconService.cs
    Services/                                   ← empty folder; implementations added per phase
    Models/                                     ← empty folder; populated in Phase 2
    Views/                                      ← empty folder; populated in Phase 4-6
    Assets/                                     ← empty folder; populated in Phase 1
tests/
  ElBruno.PresenterTimer.Tests/
    ElBruno.PresenterTimer.Tests.csproj         ← net10.0-windows + UseWPF (to ref WPF app); xUnit 2.9.3
    SanityTests.cs                              ← 5 passing tests covering ViewModelBase + RelayCommand
samples/                                        ← empty; populated in Phase 3
```

#### Target frameworks
- App: `net10.0-windows` (WPF requires `-windows` suffix). `dotnet new wpf` with `--framework net10.0` still produces `net10.0-windows` in the csproj automatically.
- Tests: `net10.0-windows` + `<UseWPF>true</UseWPF>` — required because the test project references the WPF app; without UseWPF the build fails trying to resolve WPF types.

#### Gotchas
- `dotnet new sln` in .NET 10 defaults to `.slnx` format. Use `dotnet new sln -f sln` to get the classic `.sln` file (needed for `dotnet sln add` compatibility).
- `dotnet new wpf --framework net10.0-windows` returns an error because the template only accepts bare TFMs (`net10.0`). Use `--framework net10.0`; the generated csproj will correctly say `net10.0-windows`.
- Same restriction applies to `dotnet new xunit` — use `--framework net10.0` and then manually edit the csproj to `net10.0-windows`.
- Test project needs `<UseWPF>true</UseWPF>` to compile because it references ViewModels that use `System.Windows.Input.CommandManager`.

#### Build & test commands (verified green)
```
dotnet build ElBruno.PresenterTimer.sln
dotnet test  ElBruno.PresenterTimer.sln
```
Both run from repo root (`C:\src\ElBruno.PresenterTimer`).
