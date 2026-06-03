using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Services;
using ElBruno.PresenterTimer.ViewModels;

namespace ElBruno.PresenterTimer.Tests;

public class SessionPlanEditorViewModelTests
{
    [Fact]
    public void SaveJsonCommand_ExportsEditedPlanWithReorderedSections()
    {
        var loader = new SessionLoaderService();
        var validation = new SessionValidationService();
        var dialog = new FakeFileDialogService { SavePath = @"C:\plans\edited-session.json" };
        string? savedPath = null;
        string? savedJson = null;

        var vm = new SessionPlanEditorViewModel(
            loader,
            validation,
            dialog,
            writeAllText: (path, json) =>
            {
                savedPath = path;
                savedJson = json;
            });

        vm.Title = "Edited Session";
        vm.Description = "Session from editor";
        vm.MetadataAuthor = "El Bruno";
        vm.MetadataVersion = "1.0";
        vm.MetadataCreatedAt = "2026-06-02";

        vm.SelectedSection!.Title = "Intro";
        vm.SelectedSection.DurationText = "00:03:00";
        vm.SelectedSection.WarningAtText = "00:01:00";
        vm.SelectedSection.Color = "#4CAF50";
        vm.SelectedSection.Notes = "Start";

        vm.AddSectionCommand.Execute(null);
        vm.SelectedSection!.Title = "Demo";
        vm.SelectedSection.DurationText = "00:10:00";
        vm.SelectedSection.WarningAtText = "00:02:00";
        vm.SelectedSection.Color = "#2196F3";
        vm.SelectedSection.Notes = "Main section";

        vm.MoveSectionUpCommand.Execute(null);
        vm.SaveJsonCommand.Execute(null);

        Assert.Equal(@"C:\plans\edited-session.json", savedPath);
        Assert.NotNull(savedJson);

        var parsed = loader.TryParse(savedJson!);
        Assert.Equal("Edited Session", parsed.Title);
        Assert.Equal("Session from editor", parsed.Description);
        Assert.NotNull(parsed.Metadata);
        Assert.Equal("El Bruno", parsed.Metadata!.Author);
        Assert.Equal("1.0", parsed.Metadata.Version);
        Assert.Equal("2026-06-02", parsed.Metadata.CreatedAt);
        Assert.Equal(2, parsed.Sections.Count);
        Assert.Equal("Demo", parsed.Sections[0].Title);
        Assert.Equal("Intro", parsed.Sections[1].Title);
        Assert.False(vm.HasValidationIssues);
    }

    [Fact]
    public void InvalidColor_RefreshesValidationFeedbackUsingValidationServiceMessages()
    {
        var vm = CreateViewModel();
        vm.SelectedSection!.Color = "red";

        Assert.True(vm.HasValidationIssues);
        Assert.Contains("invalid color", vm.ValidationSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveJsonCommand_DoesNotWriteWhenDurationFormatIsInvalid()
    {
        var dialog = new FakeFileDialogService { SavePath = @"C:\plans\bad-session.json" };
        bool writeCalled = false;
        var vm = new SessionPlanEditorViewModel(
            new SessionLoaderService(),
            new SessionValidationService(),
            dialog,
            writeAllText: (_, _) => writeCalled = true);

        vm.SelectedSection!.DurationText = "not-a-timespan";
        vm.SaveJsonCommand.Execute(null);

        Assert.False(writeCalled);
        Assert.True(vm.HasValidationIssues);
        Assert.Contains("invalid duration", vm.ValidationSummary, StringComparison.OrdinalIgnoreCase);
    }

    private static SessionPlanEditorViewModel CreateViewModel()
    {
        return new SessionPlanEditorViewModel(
            new SessionLoaderService(),
            new SessionValidationService(),
            new FakeFileDialogService());
    }

    private sealed class FakeFileDialogService : IFileDialogService
    {
        public string? OpenPath { get; set; }
        public string? SavePath { get; set; }

        public string? ShowOpenJsonDialog() => OpenPath;
        public string? ShowSaveJsonDialog(string? defaultFileName = null) => SavePath;
        public string? ShowSaveMarkdownDialog(string? defaultFileName = null) => null;
    }
}
