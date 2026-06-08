using System.Windows;
using System.Windows.Media;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.ViewModels;

namespace ElBruno.PresenterTimer.Tests;

public sealed class MiniOverlayViewModelSpeechAnalysisTests
{
    [Fact]
    public async Task ToggleSpeechAnalysisAsync_StartsAndStopsService()
    {
        var speechService = new FakeSpeechAnalysisService();
        using var vm = CreateViewModel(speechService);

        await vm.ToggleSpeechAnalysisAsync();
        Assert.True(vm.IsSpeechAnalysisActive);

        await vm.ToggleSpeechAnalysisAsync();
        Assert.False(vm.IsSpeechAnalysisActive);
    }

    [Fact]
    public void AnalysisReceived_ShowsInsightMessageAndColor()
    {
        var speechService = new FakeSpeechAnalysisService();
        using var vm = CreateViewModel(speechService);

        speechService.RaiseAnalysis(new AnalysisEventArgs
        {
            TranscribedText = "content",
            TopicRelevanceScore = 0.91d,
            IsOnTopic = true,
            Insight = "📊 91% on-topic",
            NextSectionPreview = ["Demo"],
            Timestamp = DateTime.UtcNow
        });

        Assert.Equal("📊 91% on-topic", vm.InsightMessage);
        Assert.Equal(Visibility.Visible, vm.InsightVisibility);
        Assert.Equal(Brushes.LightGreen, vm.InsightColor);
    }

    [Fact]
    public void SpeechAlert_ShowsErrorInsight()
    {
        var speechService = new FakeSpeechAnalysisService();
        using var vm = CreateViewModel(speechService);

        speechService.RaiseAlert(new AlertEventArgs
        {
            AlertType = AlertType.ManualSectionChange,
            Message = "Microphone unavailable.",
            SectionIndex = 0
        });

        Assert.Equal("Microphone unavailable.", vm.InsightMessage);
        Assert.Equal(Brushes.OrangeRed, vm.InsightColor);
        Assert.Equal(Visibility.Visible, vm.InsightVisibility);
    }

    [Fact]
    public void ApplySectionChange_UpdatesSpeechContext()
    {
        var speechService = new FakeSpeechAnalysisService();
        using var vm = CreateViewModel(speechService);

        speechService.LastPlan = null;
        speechService.LastSectionIndex = -1;

        typeof(MiniOverlayViewModel)
            .GetMethod("ApplySectionChange", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(vm, [1]);

        Assert.NotNull(speechService.LastPlan);
        Assert.Equal(1, speechService.LastSectionIndex);
    }

    private static MiniOverlayViewModel CreateViewModel(FakeSpeechAnalysisService speechService)
    {
        var plan = new SessionPlan
        {
            Title = "Build 2026",
            Sections =
            [
                new SessionSection { Title = "Intro", Duration = TimeSpan.FromMinutes(5) },
                new SessionSection { Title = "Architecture", Duration = TimeSpan.FromMinutes(10) },
                new SessionSection { Title = "Demo", Duration = TimeSpan.FromMinutes(8) }
            ]
        };

        return new MiniOverlayViewModel(
            new StubTimerService { Plan = plan },
            plan,
            new AppSettings { SpeechAnalysis = { Enabled = true } },
            System.Windows.Threading.Dispatcher.CurrentDispatcher,
            speechService);
    }

    private sealed class FakeSpeechAnalysisService : ISpeechAnalysisService
    {
        public bool IsListening { get; private set; }
        public SessionPlan? LastPlan { get; set; }
        public int LastSectionIndex { get; set; }

        public event EventHandler<TranscriptionEventArgs>? TranscriptionReceived;
        public event EventHandler<AnalysisEventArgs>? AnalysisReceived;
        public event EventHandler<AlertEventArgs>? AlertRaised;

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

        public void UpdatePresentationContext(SessionPlan? plan, int currentSectionIndex)
        {
            LastPlan = plan;
            LastSectionIndex = currentSectionIndex;
        }

        public void RaiseAnalysis(AnalysisEventArgs args) => AnalysisReceived?.Invoke(this, args);
        public void RaiseAlert(AlertEventArgs args) => AlertRaised?.Invoke(this, args);
        public void Dispose() { }
    }
}
