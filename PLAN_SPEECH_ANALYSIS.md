# Speech Analysis Feature Plan - ElBruno.PresenterTimer

## Overview
Add AI-powered real-time speech analysis to match presenter speech with session sections, with insights about topic relevance and upcoming sections.

## Architecture

### Libraries & Dependencies
- **ElBruno.Whisper** - Local speech-to-text (transcription)
- **ElBruno.LocalLLMs** - Local LLM inference (content analysis)
- **Azure OpenAI SDK** - Cloud-based models (alternative to local)
- **NAudio** - Audio capture from microphone
- **System.Speech** (Windows native) - Fallback audio input

### Components to Create

1. **SpeechAnalysisService** (new)
   - Manages microphone input capture
   - Handles speech-to-text conversion
   - Coordinates with LLM for content analysis
   - Runs on background thread to avoid UI blocking

2. **SpeechAnalysisViewModel** (new)
   - Exposes speech analysis state to UI
   - Manages feature enable/disable
   - Publishes insights and alerts
   - Binds to mini window UI

3. **SpeechAnalysisSettings** (extend AppSettings)
   - `EnableSpeechAnalysis: bool` - Feature on/off
   - `SpeechModelType: enum` (Local | AzureOpenAI | Foundry)
   - `LocalModelPath: string` - Path to local Whisper model
   - `AzureOpenAIEndpoint: string` - Azure endpoint URL
   - `AzureOpenAIKey: string` - API key (secrets management)
   - `FoundryProjectId: string` - Project ID for Foundry
   - `AnalysisSensitivity: enum` (Low | Medium | High) - How strict topic matching is
   - `ShowTopicRelevance: bool` - Display relevance indicator
   - `ShowNextSectionPreview: bool` - Show hint about next section

4. **SpeechAnalysisService Interface**
   ```csharp
   public interface ISpeechAnalysisService : IDisposable
   {
       Task StartListeningAsync();
       Task StopListeningAsync();
       bool IsListening { get; }
       
       event EventHandler<TranscriptionEventArgs>? TranscriptionReceived;
       event EventHandler<AnalysisEventArgs>? AnalysisReceived;
       event EventHandler<AlertEventArgs>? AlertRaised;
   }
   ```

### Data Models

```csharp
public record TranscriptionEventArgs(
    string TranscribedText,
    double Confidence,
    DateTime Timestamp);

public record AnalysisResult(
    string TranscribedText,
    double TopicRelevanceScore,  // 0.0-1.0
    bool IsOnTopic,
    string Insight,              // "Keep on track" / "Off topic" / "Great coverage"
    List<string> NextSectionPreview);

public record AnalysisEventArgs(AnalysisResult Result, DateTime Timestamp);
```

---

## Phase 1: UI Integration (v0.9.0)

**Goal:** Add UI elements and settings; no backend speech analysis yet.

### Tasks

#### 1.1 Update MiniOverlayWindow.xaml
- Add microphone button (🎤) to header row, next to close button
- Button styling: transparent, gray by default, red when active/listening
- Position: left side of close button with spacing
- Tooltip: "Enable speech analysis (off)"
- ToolTip toggles when listening: "Disable speech analysis (listening...)"

#### 1.2 Update MiniOverlayWindow.xaml.cs
- Add `OnMicrophoneToggleClick()` event handler
- Toggle `IsSpeechAnalysisActive` binding on ViewModel
- Visual feedback: button color changes when listening
- Add method to request focus on microphone (accessibility)

#### 1.3 Update MiniOverlayViewModel
- Add property: `bool IsSpeechAnalysisActive` (backing field `_isSpeechAnalysisActive`)
- Add method: `void ToggleSpeechAnalysis()` 
- Manage state: when toggled, call app-level service (stubbed for Phase 1)
- Publish state via property changes (for button binding)

#### 1.4 Update AppSettings Model
- Add `SpeechAnalysisSettings` property (new nested class)
  ```csharp
  public class SpeechAnalysisSettings
  {
      public bool Enabled { get; set; } = false;
      public string ModelType { get; set; } = "Local"; // "Local" | "AzureOpenAI" | "Foundry"
      public string LocalModelPath { get; set; } = "";
      public string AzureOpenAIEndpoint { get; set; } = "";
      public string AzureOpenAIKey { get; set; } = "";
      public string AnalysisSensitivity { get; set; } = "Medium"; // "Low" | "Medium" | "High"
      public bool ShowTopicRelevance { get; set; } = true;
      public bool ShowNextSectionPreview { get; set; } = true;
  }
  ```
- Persist to `%AppData%\ElBruno.PresenterTimer\settings.json`

#### 1.5 Update SettingsWindow.xaml
- Add new "Speech Analysis" tab in Settings
- Sections:
  - **Enable Feature** - Toggle switch for speech analysis
  - **Model Selection** - Radio buttons: Local / Azure OpenAI / Foundry
  - **Local Model Configuration** (shown when "Local" selected)
    - Path picker for Whisper model file
    - Status indicator: "Not installed" / "Ready"
    - Download link to ElBruno.Whisper docs
  - **Azure OpenAI Configuration** (shown when "Azure OpenAI" selected)
    - Endpoint URL text box
    - API Key text box (masked)
    - Test Connection button
  - **Foundry Configuration** (shown when "Foundry" selected)
    - Project ID text box
    - Environment selector (dev/prod)
  - **Analysis Sensitivity** - Slider (Low / Medium / High)
  - **Display Options** - Checkboxes
    - Show topic relevance score
    - Show next section preview

#### 1.6 Update SettingsViewModel
- Add `SpeechAnalysisSettings` properties bound to UI
- Add methods:
  - `void BrowseLocalModelPath()` - File picker for model
  - `async Task TestAzureConnectionAsync()` - Validate Azure credentials
  - `async Task TestFoundryConnectionAsync()` - Validate Foundry project
- Add validation: require model path if Local selected, endpoint/key if Azure, etc.

#### 1.7 Update App.xaml.cs
- Initialize speech analysis service as disabled (Phase 2 will activate)
- Wire up SettingsWindow changes to apply speech settings on save
- Stub: Create placeholder `SpeechAnalysisService` (non-functional for Phase 1)

#### 1.8 Create SpeechAnalysisService Stub
```csharp
public class SpeechAnalysisService : ISpeechAnalysisService
{
    // Phase 1: Do nothing, just track enabled state
    public bool IsListening { get; private set; }
    
    public Task StartListeningAsync()
    {
        IsListening = true;
        return Task.CompletedTask;
    }
    
    public Task StopListeningAsync()
    {
        IsListening = false;
        return Task.CompletedTask;
    }
    
    public void Dispose() { }
}
```

### UI Layout Changes

#### Mini Window Header (Row 0)
```
[Session]  [Microphone Button 🎤]  [Close Button ✕]
```

#### Settings Window - Speech Analysis Tab
```
┌─ Speech Analysis ──────────────────────┐
│                                         │
│ ☐ Enable Speech Analysis               │
│                                         │
│ Model Selection:                        │
│  ◯ Local    ◯ Azure OpenAI  ◯ Foundry │
│                                         │
│ [Configuration Panel - changes per model] │
│                                         │
│ Analysis Sensitivity:                   │
│ Low ▓░░░░░░░░░░░░░░░░░░░░ High        │
│                                         │
│ ☐ Show topic relevance score           │
│ ☐ Show next section preview            │
│                                         │
│ [Save] [Cancel]                        │
└─────────────────────────────────────────┘
```

### Tests (Phase 1)
- Unit tests for `SpeechAnalysisSettings` serialization/deserialization
- Unit tests for `SettingsViewModel` properties and validation
- Verify microphone button click toggles `IsSpeechAnalysisActive` in mini window
- Verify settings persist to `settings.json`

### Release Notes
```
v0.9.0 - Speech Analysis UI Foundation
- Add microphone button to mini window header (disabled, Phase 2 activation)
- Add "Speech Analysis" tab to Settings window
- Configure model type: Local, Azure OpenAI, or Foundry
- Set analysis sensitivity and display preferences
- Foundation for Phase 2 real-time speech-to-text and content analysis
```

---

## Phase 2: Full Implementation (No NuGet Publish)

**Goal:** Implement real-time speech capture, transcription, and content analysis.

### 2.1 SpeechAnalysisService Implementation

#### Audio Capture
- Use NAudio to capture microphone input in background thread
- 16 kHz mono PCM format (standard for Whisper)
- Circular buffer for streaming (no large memory footprint)
- Handle microphone device selection (default or user-selected)

#### Whisper Integration (Local)
- Load ElBruno.Whisper model on service init
- Stream audio chunks to Whisper for near-real-time transcription
- Emit `TranscriptionReceived` event for each utterance
- Confidence scoring per utterance

#### Azure OpenAI Integration
- Use Azure Cognitive Services Speech API
- Continuous speech recognition mode
- Handle token refresh for long-running sessions
- Fallback to local if cloud fails

#### Foundry Integration
- Use Foundry hosted models for speech and analysis
- Route audio → transcription → analysis in single pipeline
- Handle project-based authentication

### 2.2 Content Analysis (LLM)

#### Analysis Pipeline
1. Receive transcribed text
2. Query current section content (from session plan)
3. Build context: current section + next 2-3 sections
4. Prompt LLM: "Is this speech on-topic for {section}? Rate relevance 0-1."
5. Parse response: relevance score, topic assessment, next section hints
6. Emit `AnalysisReceived` event with results

#### Prompt Engineering
```
System: You are a presentation coach analyzing speaker content.

User: 
Session Section: "Microservices Architecture"
Next Sections: "API Design", "Deployment Strategies"
Speaker said: "So we have microservices that talk to each other..."

Analyze:
1. Is this on-topic? (yes/no)
2. Relevance score 0-1
3. Brief insight (1 sentence)
4. Suggested next topic if off-track

Format JSON response.
```

### 2.3 Mini Window UI Updates

#### Insight Display (New Row in Grid)
- Show relevance score: "📊 95% on-topic"
- Status indicators: ✅ (on-topic), ⚠️ (drifting), ❌ (off-topic)
- Next section hint: "→ API Design coming next"
- Auto-hide after 5 seconds if no new analysis

#### Microphone Button State
- Gray (off) - speech analysis disabled
- Blue (listening) - active, waiting for speech
- Green (analyzing) - processing transcription
- Red (alert) - off-topic detected
- Orange (warning) - drifting from topic

### 2.4 Settings Integration

#### Model Path Management
- Validate local Whisper model exists and is loadable
- Download button: link to ElBruno.Whisper setup docs
- Status display: "Ready" / "Not installed" / "Error: {reason}"

#### Cloud Credentials
- Test buttons: validate connectivity before saving
- Secure storage: use Windows Credential Manager for secrets (Phase 3 consideration)

#### Performance Tuning
- Batch analysis (every 10 utterances vs. immediate)
- Confidence threshold: only analyze high-confidence transcriptions
- Skip analysis if silence > 3 seconds

### 2.5 Error Handling & Fallbacks
- Microphone unavailable → show toast notification
- Whisper model missing → disable button with tooltip
- Azure/Foundry unreachable → fallback to local or disable
- Bad transcription confidence → suppress analysis

### 2.6 Accessibility
- Screen reader support for relevance score and status
- Keyboard navigation: Alt+M to toggle microphone
- Visual + audible feedback for on/off-topic

### 2.7 Tests (Phase 2)
- Unit tests for `SpeechAnalysisService.StartListening()` / `StopListening()`
- Mock transcription events and verify LLM is called
- Mock LLM responses and verify UI updates
- Integration tests: local Whisper + ElBruno.LocalLLMs
- Performance tests: latency from speech → insight display

### 2.8 Documentation
- README section: "Speech Analysis"
- Setup guide for local vs. cloud models
- Troubleshooting: microphone permissions, model downloads, API keys

---

## Dependencies & NuGet Packages

| Package | Version | Purpose | Phase |
|---------|---------|---------|-------|
| ElBruno.Whisper | Latest | Local speech-to-text | 2 |
| ElBruno.LocalLLMs | Latest | Local LLM inference | 2 |
| Azure.AI.OpenAI | Latest | Azure OpenAI API | 2 |
| NAudio | 2.2+ | Microphone input | 2 |
| Azure.Storage.Blobs | Latest | Foundry integration (if needed) | 2 |

---

## Timeline

- **Phase 1 (v0.9.0):** 1-2 hours (UI + settings + stub service)
- **Phase 2:** 4-6 hours (audio capture, Whisper, LLM integration, testing)

---

## Risk & Mitigations

| Risk | Mitigation |
|------|-----------|
| Microphone permission denied | Graceful fallback, clear error message, link to OS settings |
| Whisper model slow on first load | Cache model in memory, show loading indicator |
| LLM latency high | Batch analysis, debounce rapid speech events |
| False positives (off-topic) | Sensitivity slider, confidence threshold, user feedback loop |
| Privacy concerns (audio capture) | Document what is captured, options to use local-only models |

---

## Future Enhancements (Phase 3+)

- Speaker notes integration: auto-advance to next section if detected
- Analytics dashboard: topic coverage % over time
- Real-time transcription display (live caption view)
- Multi-speaker support: identify who's speaking
- Language support: detect speaker language, translate if needed
- Model fine-tuning: learn presentation patterns over time
