# ElBruno.PresenterTimer

**ElBruno.PresenterTimer** (Session Timeline Overlay) is a lightweight Windows desktop application for presenters, content creators, trainers, and speakers who need to stay on track during recordings, demos, conferences, or workshops — without constantly switching windows or checking a separate clock.

The app sits in the Windows system tray and projects a subtle, always-on-top horizontal timeline overlay that shows your full session plan divided into sections. As time passes the current section is highlighted, completed sections are visually differentiated, and configurable alerts fire when a section is almost over or the session goes into overtime.

---

## Features

- **System tray app** — minimal footprint; lives in the Windows notification area.
- **Always-on-top overlay** — a horizontal timeline bar placed anywhere on screen, ideal for multi-monitor setups and OBS/screen-recording scenarios.
- **JSON-driven session plans** — define your session title, sections, durations, and notes in a simple JSON file.
- **Live section tracking** — current section is highlighted; progress is shown across the full session.
- **Visual state indicators** — tray icon changes colour (gray / blue / green / yellow / red) to reflect app state at a glance.
- **Configurable alerts** — warning and overtime alerts with optional audio cues.
- **Timer controls** — start, pause, resume, reset, next/previous section, restart current section, and extend by +1 or +5 minutes.
- **Recent sessions** — quick reload of previously used session files.
- **Session preview & summary** — review the plan before starting and get a recap after finishing.
- **Settings persistence** — preferences saved to `%AppData%`.

---

## Prerequisites

| Requirement | Version |
|---|---|
| Windows | 10 or later (64-bit) |
| .NET SDK | 10.0 or later |
| IDE (optional) | Visual Studio 2022 / Rider / VS Code + C# Dev Kit |

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/elbruno/ElBruno.PresenterTimer.git
cd ElBruno.PresenterTimer
```

### 2. Build

```bash
dotnet build src/SessionTimelineOverlay/SessionTimelineOverlay.csproj
```

### 3. Run

```bash
dotnet run --project src/SessionTimelineOverlay/SessionTimelineOverlay.csproj
```

The application will appear in the Windows system tray.

### 4. Run tests

```bash
dotnet test tests/SessionTimelineOverlay.Tests/SessionTimelineOverlay.Tests.csproj
```

---

## Usage

### Prepare a session JSON file

Create a `.json` file describing your session:

```json
{
  "title": "AI Agents Demo",
  "description": "Recording plan for a technical demo",
  "sections": [
    { "title": "Intro",             "duration": "00:03:00", "notes": "Welcome and context" },
    { "title": "Problem Statement", "duration": "00:05:00", "notes": "Explain the scenario" },
    { "title": "Demo",              "duration": "00:15:00", "notes": "Show the app running" },
    { "title": "Wrap-up",           "duration": "00:04:00", "notes": "Summary and links" }
  ]
}
```

### Load and start

1. Right-click the tray icon → **Import Session JSON** → select your file.
2. A **Session Preview** window shows the plan.
3. Click **Start Session** (or use the tray menu) to begin the countdown.
4. The timeline overlay appears automatically (configurable in **Settings → Behavior**).

### During the session

- The current section is highlighted in the overlay.
- The tray icon turns **yellow** when a section is almost over, and **red** if you go into overtime.
- Use the tray context menu to navigate sections, pause/resume, or extend time.

---

## Project Structure

```text
src/
  SessionTimelineOverlay/
    App.xaml / Program.cs
    Views/          ← WPF windows
    ViewModels/     ← MVVM view models
    Services/       ← Timer, tray, alerts, settings, hotkeys
    Models/         ← SessionPlan, AppSettings, …
    Abstractions/   ← Interfaces for testability
tests/
  SessionTimelineOverlay.Tests/
docs/
  SessionTimelineOverlay_PRD.md   ← Full product requirements
```

---

## License

This project is licensed under the [MIT License](LICENSE).

---

## Author

Built by **Bruno Capuano** ([@elbruno](https://github.com/elbruno)) — El Bruno.
