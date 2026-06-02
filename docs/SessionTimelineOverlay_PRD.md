# PRD: Session Timeline Overlay

## 1. Product Overview

**Session Timeline Overlay** is a Windows desktop application designed to help presenters, content creators, trainers, podcasters, and demo-heavy speakers manage recording or presentation timing using a visual timeline overlay.

The application runs from the Windows system tray and displays a subtle, configurable, always-on-top overlay showing the full session timeline. The timeline is divided into sections loaded from a JSON configuration file. As time progresses, the overlay highlights the current section, shows progress across the whole session, and provides configurable visual and optional audio alerts when a section is close to ending or when the session goes overtime.

The main goal is to make it easy to stay on track during recordings, demos, conference sessions, workshops, and keynote-style presentations without constantly switching windows or checking a separate timer.

---

## 2. Goals

- Provide a lightweight Windows systray app for managing recording and presentation timing.
- Load session plans from a JSON file.
- Show a full timeline overlay divided into N configured sections.
- Highlight the current section visually.
- Provide configurable alerts for section warnings, section end, and overtime.
- Allow the user to start, pause, resume, reset, and manually navigate sections.
- Provide style and behavior settings for overlay appearance and app behavior.
- Support presenter-friendly scenarios like OBS, screen recording, demos, and multi-monitor setups.
- Keep the first version simple, useful, and reliable.

---

## 3. Non-Goals

The first version should **not** try to become a full video production tool.

Out of scope for MVP:

- Video recording.
- Audio recording.
- Editing video timelines.
- Cloud synchronization.
- Real-time collaboration.
- AI-generated session plans.
- Integration with PowerPoint, Teams, OBS APIs, or streaming platforms.
- Mobile companion app.

These can be considered in later versions.

---

## 4. Target Users

### Primary Users

- Technical presenters.
- Developer advocates.
- Trainers.
- YouTubers.
- Podcasters.
- Workshop facilitators.
- Conference speakers.
- People recording structured video content.

### Example User Story

> As a presenter recording a 30-minute technical demo, I want to see a subtle full-session timeline overlay divided by my planned sections, so I know where I am, what comes next, and when I need to wrap up the current section.

---

## 5. Core Product Concept

The user prepares a JSON session file like this:

```json
{
  "title": "AI Agents Recording",
  "description": "Recording plan for a technical demo",
  "sections": [
    {
      "title": "Intro",
      "duration": "00:03:00",
      "notes": "Welcome and context"
    },
    {
      "title": "Problem Statement",
      "duration": "00:05:00",
      "notes": "Explain the scenario"
    },
    {
      "title": "Demo",
      "duration": "00:15:00",
      "notes": "Show the app running locally"
    },
    {
      "title": "Wrap-up",
      "duration": "00:04:00",
      "notes": "Summary and links"
    }
  ]
}
```

The app imports this file and renders a horizontal full-session timeline overlay:

```text
| Intro | Problem Statement | Demo | Wrap-up |
                  ▲
         Current session progress
```

The timeline should visually show:

- Full session progress.
- Section boundaries.
- Current section highlighted.
- Completed sections visually differentiated.
- Upcoming sections visible but less prominent.
- Warning state when the current section is close to ending.
- Overtime state when a section or session exceeds the planned duration.

---

## 6. App Architecture Recommendation

### Recommended Technology

- **.NET 10**
- **WPF** for desktop UI.
- Windows systray support through a small tray abstraction.
- MVVM pattern.
- JSON serialization using `System.Text.Json`.
- Settings persisted to `%AppData%`.

### Suggested Project Structure

```text
src/
  SessionTimelineOverlay/
    App.xaml
    Program.cs
    Views/
      TimelineOverlayWindow.xaml
      SettingsWindow.xaml
      SessionPreviewWindow.xaml
      SessionSummaryWindow.xaml
    ViewModels/
      TimelineOverlayViewModel.cs
      SettingsViewModel.cs
      SessionPreviewViewModel.cs
      SessionSummaryViewModel.cs
    Services/
      SessionLoaderService.cs
      SessionValidationService.cs
      SessionTimerService.cs
      SettingsService.cs
      AlertService.cs
      TrayIconService.cs
      HotkeyService.cs
      WindowPlacementService.cs
    Models/
      SessionPlan.cs
      SessionSection.cs
      AppSettings.cs
      AlertSettings.cs
      OverlayStyleSettings.cs
      OverlayBehaviorSettings.cs
    Abstractions/
      ITrayIconService.cs
      ISettingsService.cs
      ISessionTimerService.cs
      IAlertService.cs
      IFileDialogService.cs
      ISystemNotificationService.cs

tests/
  SessionTimelineOverlay.Tests/
```

---

## 7. Functional Requirements

## 7.1 System Tray Behavior

The app must run primarily from the Windows system tray.

### Tray Icon States

The tray icon should visually indicate the app state:

| State | Meaning |
|---|---|
| Gray | No session loaded |
| Blue | Session loaded but not started |
| Green | Session running |
| Yellow | Warning: current section is almost done |
| Red | Overtime |
| Paused indicator | Session paused |

### Tray Menu

The systray context menu should include:

```text
Session Timeline Overlay

Start Session
Pause Session / Resume Session
Reset Session

Next Section
Previous Section
Restart Current Section
Extend Current Section
  +1 minute
  +5 minutes

Import Session JSON
Reload Last Session
Recent Sessions
Export Sample JSON

Show Timeline Overlay
Hide Timeline Overlay

Open Session Preview
Open Session Summary
Settings
About
Exit
```

### Start Session Behavior

When the user selects **Start Session**:

1. If no session is loaded, prompt the user to import a JSON session file.
2. If a session is loaded, start the timer.
3. If the timeline overlay is not currently visible, the app should show it automatically **if the setting is enabled**.

This behavior must be configurable:

```text
Settings > Behavior > Show timeline overlay when session starts
Default: true
```

If the setting is disabled, Start Session should start the timer without forcing the overlay to appear.

---

## 7.2 Import Session JSON

The user must be able to import a JSON file from the systray menu or from the Session Preview window.

After import:

1. Parse JSON.
2. Validate the session structure.
3. Show a preview before starting.
4. Save the file path as the last loaded session.
5. Add it to recent sessions.

---

## 7.3 Session JSON Schema

### MVP JSON Format

```json
{
  "title": "AI Agents Recording",
  "description": "Recording plan for a technical demo",
  "sections": [
    {
      "title": "Intro",
      "duration": "00:03:00",
      "notes": "Welcome and context"
    },
    {
      "title": "Demo",
      "duration": "00:15:00",
      "notes": "Main technical demo"
    },
    {
      "title": "Wrap-up",
      "duration": "00:04:00",
      "notes": "Summary and call to action"
    }
  ]
}
```

### Extended JSON Format

```json
{
  "title": "AI Agents Recording",
  "description": "Recording plan for a technical demo",
  "metadata": {
    "author": "Bruno Capuano",
    "version": "1.0",
    "createdAt": "2026-06-02"
  },
  "sections": [
    {
      "title": "Intro",
      "duration": "00:03:00",
      "notes": "Welcome and context",
      "color": "#4CAF50",
      "warningAt": "00:01:00"
    },
    {
      "title": "Demo",
      "duration": "00:15:00",
      "notes": "Main technical demo",
      "color": "#2196F3",
      "warningAt": "00:02:00"
    }
  ]
}
```

### Required Fields

| Field | Required | Notes |
|---|---:|---|
| `title` | Yes | Session title |
| `description` | No | Optional session description |
| `sections` | Yes | Must contain at least one section |
| `sections[].title` | Yes | Section title |
| `sections[].duration` | Yes | Format: `HH:mm:ss` |
| `sections[].notes` | No | Optional presenter notes |
| `sections[].color` | No | Optional section color |
| `sections[].warningAt` | No | Optional per-section warning threshold |

---

## 7.4 JSON Validation

The app must validate imported JSON files.

Validation rules:

- JSON must be valid.
- Session title must not be empty.
- Sections array must exist.
- Sections array must contain at least one section.
- Every section must have a title.
- Every section must have a valid duration.
- Section duration must be greater than zero.
- Total duration must be greater than zero.
- Optional colors must be valid hex colors.
- Optional `warningAt` must be less than section duration.

Validation error example:

```text
Invalid session file.

Section 3, "Demo", has a warning time of 00:06:00 but the section duration is only 00:05:00.
```

---

## 7.5 Session Preview

After importing a session file, the app should show a preview window.

The preview must include:

- Session title.
- Session description.
- Total planned duration.
- Number of sections.
- Section list.
- Duration per section.
- Optional notes per section.
- Validation warnings.
- Buttons:
  - Start Session
  - Cancel
  - Import Different JSON
  - Export Normalized JSON

Example:

```text
Session: AI Agents Recording
Total Duration: 27:00
Sections: 4

1. Intro - 03:00
2. Problem Statement - 05:00
3. Demo - 15:00
4. Wrap-up - 04:00
```

---

## 7.6 Timeline Overlay

The overlay is the main product experience.

### Overlay Mode

The default mode must be:

```text
Full Timeline Mode
```

This means the overlay shows the full session timeline from beginning to end, divided into N sections based on the imported session file.

### Timeline Layout

The overlay should render a horizontal progress bar split into section segments.

Each segment width should be proportional to its duration.

Example:

```text
| Intro | Context | Demo                 | Wrap |
|-------|---------|----------------------|------|
              ██████████░░░░░░░░░░░░
```

### Current Section Highlighting

The current section should be clearly highlighted compared to other sections.

Visual states:

| Section State | Visual Behavior |
|---|---|
| Completed | Dimmed or filled |
| Current | Highlighted border, brighter background, stronger label |
| Upcoming | Lower opacity |
| Warning | Yellow/pulsing/current section emphasis |
| Overtime | Red accent, overtime label |

### Timeline Information

The overlay should show:

- Session title.
- Current section title.
- Current section elapsed time.
- Current section remaining time.
- Total session elapsed time.
- Total session remaining time.
- Next section title.
- Progress marker.

Compact example:

```text
AI Agents Recording
Current: Demo | 07:30 / 15:00 | Remaining: 07:30 | Next: Wrap-up
[Intro][Context][Demo █████░░░░░][Wrap-up]
```

Minimal example:

```text
Demo | 07:30 left | Next: Wrap-up
[Timeline bar]
```

---

## 7.7 Overlay Window Behavior

The overlay must support:

- Always on top.
- Optional click-through mode.
- Draggable mode when click-through is disabled.
- Opacity configuration.
- Position configuration.
- Size configuration.
- Monitor selection.
- Remember last position.
- Hide/show from systray.
- Safe behavior over PowerPoint, Teams, OBS, browsers, terminals, and VS Code.

### Overlay Positions

Supported positions:

- Top center.
- Bottom center.
- Top left.
- Top right.
- Bottom left.
- Bottom right.
- Custom position.

### Display Modes

| Mode | Description |
|---|---|
| Full Timeline | Full session timeline split by sections |
| Compact | Current section + short timeline |
| Minimal | Current section + remaining time only |
| Presenter | Larger text for second monitor |
| OBS Friendly | Transparent background and capture-friendly layout |

For MVP, **Full Timeline** is required. Other modes can be implemented after the core scenario works.

---

## 7.8 Alerts

The app must implement configurable alerts.

### Alert Types

| Alert | Trigger |
|---|---|
| Section Warning | Current section has X time remaining |
| Section End | Current section reaches planned duration |
| Session Warning | Whole session has X time remaining |
| Session End | Planned session duration reached |
| Overtime | Current time exceeds planned section/session duration |
| Manual Section Change | User moves to next/previous section |

### Default Alert Behavior

Default settings:

```text
Section warning: enabled, 1 minute before section end
Session warning: enabled, 3 minutes before session end
Section end alert: enabled
Session end alert: enabled
Overtime alert: enabled
Sound: disabled by default
Windows notification: disabled by default
Overlay pulse: enabled
```

### Visual Alerts

The overlay should support:

- Subtle pulse animation.
- Warning color state.
- Overtime color state.
- Temporary alert message.
- Progress marker color change.

Example:

```text
⚠️ 1 minute left in "Demo"
```

Overtime example:

```text
Overtime in "Demo": +01:42
Session behind schedule: +03:10
```

### Audio Alerts

Audio alerts should be optional.

Settings:

- Enable sound alerts.
- Section warning sound.
- Section end sound.
- Session end sound.
- Volume.
- Test sound button.

Default: sound disabled.

### Windows Notifications

Windows notifications should be optional.

Settings:

- Enable Windows notifications.
- Notify on section warning.
- Notify on section end.
- Notify on session end.
- Notify on overtime.

Default: disabled.

### Alert Deduplication

Each alert should fire only once per section unless the section is restarted.

For example:

- The 1-minute warning for `Demo` should fire once.
- If the user restarts `Demo`, it may fire again.
- If the user pauses and resumes, it should not refire immediately unless the threshold is crossed again in a valid way.

---

## 7.9 Manual Controls

The user must be able to control the session manually.

Required controls:

- Start.
- Pause.
- Resume.
- Reset.
- Next Section.
- Previous Section.
- Restart Current Section.
- Extend Current Section by 1 minute.
- Extend Current Section by 5 minutes.
- Hide overlay.
- Show overlay.

Manual controls are available from:

- Systray menu.
- Optional overlay controls when enabled.
- Optional global hotkeys.

---

## 7.10 Overtime Behavior

The app must support overtime clearly.

### Section Overtime

If the current section exceeds its planned duration and auto-advance is disabled, the app should enter section overtime mode.

Example:

```text
Demo | Overtime +01:42 | Next: Wrap-up
```

### Session Overtime

If the total planned duration is exceeded, the app should enter session overtime mode.

Example:

```text
Session overtime: +03:21
```

### Behind Schedule Calculation

The app should calculate how far behind the session is compared to the original plan.

Example:

```text
Behind schedule: +02:15
```

---

## 7.11 Auto-Advance Behavior

The app should support two modes:

### Manual Section Mode

The app stays in the current section until the user manually advances.

This is useful for recordings where the user wants flexibility.

### Auto-Advance Mode

The app automatically moves to the next section when the current section reaches its planned duration.

This is useful for strict rehearsals or live sessions.

Default:

```text
Auto-advance sections: false
```

This should be configurable in settings.

---

## 7.12 Settings

Settings should be available from the systray menu.

Settings must be persisted to:

```text
%AppData%\SessionTimelineOverlay\settings.json
```

## Settings Categories

### General

| Setting | Default |
|---|---|
| Launch app minimized to tray | true |
| Remember last session | true |
| Auto-load last session on startup | false |
| Show session preview after import | true |
| Confirm before reset | true |
| Confirm before exit while session is running | true |

### Behavior

| Setting | Default |
|---|---|
| Show timeline overlay when session starts | true |
| Hide overlay when session ends | false |
| Auto-advance sections | false |
| Keep counting overtime after section end | true |
| Keep counting overtime after session end | true |
| Enable global hotkeys | false |
| Enable overlay click-through | false |
| Pause timer when computer locks | true |

### Overlay Style

| Setting | Default |
|---|---|
| Theme | System |
| Accent color | User configurable |
| Warning color | Yellow/Amber |
| Overtime color | Red |
| Completed section opacity | 45% |
| Upcoming section opacity | 55% |
| Current section opacity | 100% |
| Overlay opacity | 85% |
| Font family | Segoe UI |
| Font size | Medium |
| Border radius | Medium |
| Show section labels | true |
| Show session title | true |
| Show current section title | true |
| Show next section title | true |
| Show time remaining | true |
| Show elapsed time | true |

### Overlay Layout

| Setting | Default |
|---|---|
| Overlay mode | Full Timeline |
| Position | Top center |
| Monitor | Primary |
| Width | 80% of monitor width |
| Height | Compact |
| Remember custom position | true |
| Enable drag to move | true when click-through is disabled |
| Snap to screen edges | true |

### Alerts

| Setting | Default |
|---|---|
| Enable section warning alerts | true |
| Section warning threshold | 00:01:00 |
| Enable session warning alerts | true |
| Session warning threshold | 00:03:00 |
| Enable section end alerts | true |
| Enable session end alerts | true |
| Enable overtime alerts | true |
| Enable overlay pulse | true |
| Enable sound alerts | false |
| Enable Windows notifications | false |
| Alert message duration | 5 seconds |

### Hotkeys

Default hotkeys should be disabled to avoid conflicts.

Suggested hotkeys:

| Action | Suggested Hotkey |
|---|---|
| Pause / Resume | Ctrl + Alt + Space |
| Next Section | Ctrl + Alt + Right |
| Previous Section | Ctrl + Alt + Left |
| Reset Session | Ctrl + Alt + R |
| Show / Hide Overlay | Ctrl + Alt + H |
| Extend Current Section +1 Minute | Ctrl + Alt + Up |

---

## 7.13 Settings Window

The settings window should include:

- Tabs or sections for:
  - General
  - Behavior
  - Overlay Style
  - Overlay Layout
  - Alerts
  - Hotkeys
  - Advanced
- Save button.
- Cancel button.
- Apply button.
- Reset to defaults button.
- Open settings folder button.
- Export settings button.
- Import settings button.

Settings should apply safely.

Overlay style changes should update the overlay live when possible.

---

## 7.14 Session Summary

When a session ends, the app should optionally show a summary.

The summary should include:

- Session title.
- Planned total duration.
- Actual total duration.
- Difference between planned and actual.
- Section-level planned vs actual times.
- Overtime per section.
- Sections skipped.
- Manual extensions applied.

Example:

```text
Session completed: AI Agents Recording

Planned: 27:00
Actual: 31:20
Difference: +04:20

Intro: planned 03:00, actual 03:20, +00:20
Problem Statement: planned 05:00, actual 04:50, -00:10
Demo: planned 15:00, actual 18:35, +03:35
Wrap-up: planned 04:00, actual 04:35, +00:35
```

Export options:

- Copy summary to clipboard.
- Save summary as Markdown.
- Save summary as JSON.

---

## 7.15 Templates

The app should support templates after MVP.

Example templates:

- 10-minute demo.
- 15-minute video.
- 30-minute podcast.
- 45-minute session.
- 60-minute workshop.
- Conference talk.
- Demo-heavy talk.

Templates should be exportable as JSON.

---

## 7.16 Recent Sessions

The app should keep a list of recent session files.

Settings:

```text
Maximum recent sessions: 10
```

The systray menu should show recent sessions under:

```text
Recent Sessions
```

If a recent file no longer exists, the app should show a friendly error and optionally remove it from the list.

---

## 7.17 OBS / Recording Friendly Mode

The app should include an OBS-friendly mode.

This mode should support:

- Transparent or semi-transparent background.
- Clean visual design.
- No unnecessary buttons.
- Stable window title for capture.
- Optional separate overlay window for capture.
- Option to hide overlay from screen sharing if technically feasible.

MVP can include the visual preset only.

---

## 7.18 Multi-Monitor Support

The app should support monitor selection.

Settings:

- Show overlay on primary monitor.
- Show overlay on secondary monitor.
- Remember monitor by device name.
- Fallback to primary monitor if the saved monitor is disconnected.

---

## 8. UX Requirements

## 8.1 App Startup

On startup:

1. App starts minimized to systray by default.
2. If configured, app loads the last session.
3. Overlay is hidden by default unless the user configured otherwise.
4. Tray icon shows the current state.

## 8.2 Import Flow

```text
User clicks Import Session JSON
→ File picker opens
→ User selects file
→ App validates JSON
→ If valid, Session Preview opens
→ User clicks Start Session
→ Timer starts
→ Overlay appears if setting is enabled
```

## 8.3 Start Flow

```text
User clicks Start Session
→ If no session is loaded, ask for JSON
→ If session is loaded, start timer
→ If overlay is hidden and setting is enabled, show overlay
→ Tray icon changes to running state
```

## 8.4 Pause Flow

```text
User clicks Pause
→ Timer pauses
→ Overlay shows paused state
→ Tray icon changes to paused state
```

## 8.5 End Flow

```text
Session reaches end
→ App shows session end alert
→ If overtime is enabled, timer continues counting overtime
→ If summary is enabled, show Session Summary
```

---

## 9. Data Models

## 9.1 SessionPlan

```csharp
public sealed class SessionPlan
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SessionMetadata? Metadata { get; set; }
    public List<SessionSection> Sections { get; set; } = [];
}
```

## 9.2 SessionSection

```csharp
public sealed class SessionSection
{
    public string Title { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string? Notes { get; set; }
    public string? Color { get; set; }
    public TimeSpan? WarningAt { get; set; }
}
```

## 9.3 AppSettings

```csharp
public sealed class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public OverlayStyleSettings OverlayStyle { get; set; } = new();
    public OverlayLayoutSettings OverlayLayout { get; set; } = new();
    public AlertSettings Alerts { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
}
```

---

## 10. Technical Requirements

## 10.1 Timer Accuracy

The timer should be accurate enough for presentation timing.

Requirements:

- Use a monotonic time source where possible.
- Avoid relying only on UI timer ticks for actual elapsed time.
- UI can update every 250ms to 1000ms.
- Actual elapsed time should be calculated from start/pause/resume timestamps.

## 10.2 Performance

- App should use minimal CPU when idle.
- Overlay updates should be smooth but not expensive.
- App should not interfere with recording software or demos.
- App should not steal focus when alerts fire.

## 10.3 Reliability

- Invalid JSON should not crash the app.
- Missing recent files should not crash the app.
- Settings corruption should fallback to defaults.
- Overlay should recover if monitor layout changes.
- App should exit cleanly from systray.

## 10.4 Accessibility

- Use readable fonts.
- Support high contrast themes where possible.
- Avoid relying only on color for state.
- Provide text labels for warning and overtime states.
- Allow larger font sizes.

---

## 11. Quality Requirements

### Unit Tests

Required tests:

- JSON parsing succeeds for valid files.
- JSON parsing fails safely for invalid files.
- Session validation catches missing title.
- Session validation catches empty sections.
- Session validation catches zero duration.
- Session validation catches invalid warning thresholds.
- Timer calculates current section correctly.
- Timer calculates total elapsed and remaining time correctly.
- Timer handles pause/resume correctly.
- Timer handles manual next/previous section.
- Alert service fires warning once per section.
- Settings load defaults correctly.
- Settings persist and reload correctly.

### Integration Tests

- Import JSON → preview → start session.
- Start session shows overlay when setting is enabled.
- Start session does not show overlay when setting is disabled.
- Overlay view model updates current section.
- Overtime is calculated correctly.

### Manual Tests

- Launch app.
- Confirm tray icon appears.
- Import sample JSON.
- Start session.
- Confirm overlay appears.
- Confirm full timeline is visible.
- Confirm current section is highlighted.
- Pause/resume session.
- Move to next/previous section.
- Trigger section warning.
- Trigger overtime.
- Change style settings.
- Confirm overlay updates.
- Move overlay to another monitor.
- Exit app.
- Confirm process exits cleanly.

---

## 12. MVP Scope

The MVP should include:

1. WPF app running in systray.
2. Import session JSON.
3. Validate session JSON.
4. Session preview window.
5. Start, pause, resume, reset.
6. Show/hide timeline overlay.
7. Start Session optionally shows overlay based on settings.
8. Full timeline overlay divided into sections.
9. Current section highlighting.
10. Completed/upcoming/current visual states.
11. Section warning alert.
12. Section end alert.
13. Overtime visual state.
14. Settings for theme, colors, opacity, position, and alert behavior.
15. Save settings to `%AppData%`.
16. Reload last session.
17. Export sample JSON.
18. Session summary window.
19. Clean exit from systray.

---

## 13. Post-MVP Features

Recommended v2 features:

- Global hotkeys.
- Multi-monitor advanced support.
- OBS-friendly capture window.
- Templates.
- Visual session editor.
- Import/export settings.
- Export session summary as Markdown.
- Custom sounds.
- Better animations.
- Optional compact/minimal overlay modes.
- Portable mode.
- Installer.

Recommended v3 features:

- PowerPoint integration.
- OBS integration.
- Stream Deck integration.
- Cloud sync.
- AI-assisted session plan generation.
- Speech-aware pacing suggestions.
- Real-time transcript timing.

---

## 14. Implementation Plan

## Phase 1: App Shell and Systray

Deliverables:

- WPF app shell.
- Systray icon.
- Systray menu.
- Clean app exit.
- Basic settings service.
- App folders under `%AppData%`.

Acceptance criteria:

- App launches minimized to tray.
- Tray menu opens.
- Exit closes process cleanly.
- Settings file is created with defaults.

---

## Phase 2: Session JSON Import and Validation

Deliverables:

- Session models.
- JSON loader.
- Validation service.
- File import flow.
- Export sample JSON.

Acceptance criteria:

- Valid JSON imports successfully.
- Invalid JSON shows friendly errors.
- Sample JSON exports correctly.

---

## Phase 3: Session Preview

Deliverables:

- Preview window.
- Session duration calculation.
- Section list display.
- Start button from preview.

Acceptance criteria:

- Preview shows title, total duration, and sections.
- Start button starts a loaded session.

---

## Phase 4: Timer Engine

Deliverables:

- Session timer service.
- Start/pause/resume/reset.
- Current section calculation.
- Manual next/previous section.
- Overtime calculation.

Acceptance criteria:

- Timer accurately tracks session progress.
- Current section updates correctly.
- Pause/resume preserves elapsed time.
- Overtime is calculated correctly.

---

## Phase 5: Full Timeline Overlay

Deliverables:

- Timeline overlay window.
- Full timeline rendering.
- Proportional section widths.
- Current section highlighting.
- Completed/upcoming/current styles.
- Show/hide overlay behavior.
- Start Session setting to auto-show overlay.

Acceptance criteria:

- Overlay shows full timeline.
- Timeline is divided into configured sections.
- Current section is highlighted.
- Start Session shows overlay when setting is enabled.
- Start Session does not show overlay when setting is disabled.

---

## Phase 6: Alerts

Deliverables:

- Alert service.
- Section warning alert.
- Section end alert.
- Session warning alert.
- Session end alert.
- Overtime alert.
- Overlay pulse behavior.
- Alert settings.

Acceptance criteria:

- Warning alert fires once per section.
- Section end alert fires correctly.
- Overtime visual state appears correctly.
- Alerts follow user settings.

---

## Phase 7: Settings UI

Deliverables:

- Settings window.
- General settings.
- Behavior settings.
- Overlay style settings.
- Overlay layout settings.
- Alert settings.
- Save/reload/reset settings.

Acceptance criteria:

- User can change theme, colors, opacity, and position.
- User can enable/disable auto-show overlay on start.
- User can configure alerts.
- Settings persist across app restarts.

---

## Phase 8: Session Summary

Deliverables:

- Session summary data model.
- Summary window.
- Planned vs actual duration.
- Section-level duration stats.
- Copy summary to clipboard.

Acceptance criteria:

- Summary appears after session end if enabled.
- Summary accurately shows planned vs actual times.

---

## Phase 9: Polish and Reliability

Deliverables:

- Error handling.
- Friendly messages.
- Recent sessions.
- Missing file handling.
- Monitor fallback.
- Accessibility improvements.
- Manual test pass.

Acceptance criteria:

- App does not crash on bad input.
- App exits cleanly.
- Overlay behaves properly across common apps.

---

## 15. Acceptance Criteria for MVP

The MVP is complete when:

- The app runs from the Windows systray.
- The user can import a valid JSON session file.
- Invalid session files show useful errors.
- The user can preview a session before starting.
- The user can start, pause, resume, and reset a session.
- The overlay displays a full timeline divided by configured sections.
- The current section is visually highlighted.
- Completed and upcoming sections are visually distinct.
- Start Session shows the overlay if the setting is enabled.
- Alerts work for section warning, section end, and overtime.
- The user can configure style settings.
- The user can configure alert settings.
- Settings persist between launches.
- The app exits cleanly.

---

## 16. Suggested Product Names

Possible names:

- SessionPilot
- Timeline Buddy
- RecordBuddy
- TimePilot
- DemoTimer
- SessionFlow
- ElBruno.SessionTimer
- ElBruno.TimelineOverlay
- PresenterTimeline
- RecordingPilot

Recommended name for the first implementation:

```text
SessionPilot
```

Why:

- Short.
- Easy to remember.
- Works for recordings, demos, talks, and workshops.
- Does not over-specialize the product.

---

## 17. Example Sample JSON File

```json
{
  "title": "Build Recording - AI Agents Demo",
  "description": "A structured recording session for a technical video.",
  "sections": [
    {
      "title": "Intro",
      "duration": "00:03:00",
      "notes": "Welcome, goal of the video, what the viewer will learn.",
      "color": "#4CAF50",
      "warningAt": "00:01:00"
    },
    {
      "title": "Context",
      "duration": "00:05:00",
      "notes": "Explain the problem, tools, and architecture.",
      "color": "#2196F3",
      "warningAt": "00:01:00"
    },
    {
      "title": "Demo",
      "duration": "00:15:00",
      "notes": "Show the app running locally and explain the key parts.",
      "color": "#9C27B0",
      "warningAt": "00:02:00"
    },
    {
      "title": "Wrap-up",
      "duration": "00:04:00",
      "notes": "Summarize, share links, and close.",
      "color": "#FF9800",
      "warningAt": "00:01:00"
    }
  ]
}
```

---

## 18. Open Questions

- Should the app auto-start with Windows?
- Should the overlay appear in screen recordings by default, or should the app make it easy to keep it private?
- Should the timer auto-advance sections by default or stay manual by default?
- Should section extensions modify only the runtime session or also update the JSON plan?
- Should the app support multiple overlays at the same time, for example one private presenter overlay and one OBS capture overlay?
- Should the app support keyboard-only operation for accessibility?

Recommended MVP decisions:

- Auto-start with Windows: no.
- Overlay visible in recordings: yes, unless hidden by the user.
- Auto-advance: no.
- Section extensions: runtime only.
- Multiple overlays: no for MVP.
- Keyboard-only support: partial for MVP, improve later.
