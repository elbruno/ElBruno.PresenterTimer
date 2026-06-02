using System.IO;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;
using ElBruno.PresenterTimer.Services;

namespace ElBruno.PresenterTimer.Tests;

/// <summary>
/// Unit tests for <see cref="RecentSessionsService"/>.
/// All tests use an injected file-exists predicate — no real disk I/O required.
/// </summary>
public class RecentSessionsServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Minimal ISettingsService that stores a live AppSettings in memory.</summary>
    private sealed class StubSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public event EventHandler? SettingsApplied;
        public void Load() { }
        public void Save() { /* no-op — tests inspect Settings directly */ }
        public void RaiseSettingsApplied() => SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    private static RecentSessionsService MakeService(
        StubSettingsService settings,
        HashSet<string>? existingFiles = null)
    {
        existingFiles ??= [];
        return new RecentSessionsService(settings,
            path => existingFiles.Contains(path, StringComparer.OrdinalIgnoreCase));
    }

    // ── Add ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_SinglePath_AppearsInGetAll()
    {
        var svc = MakeService(new StubSettingsService());

        svc.Add(@"C:\sessions\demo.json");

        Assert.Single(svc.GetAll());
        Assert.Equal(@"C:\sessions\demo.json", svc.GetAll()[0]);
    }

    [Fact]
    public void Add_UpdatesLastSessionPath()
    {
        var stub = new StubSettingsService();
        var svc = MakeService(stub);

        svc.Add(@"C:\sessions\demo.json");

        Assert.Equal(@"C:\sessions\demo.json", stub.Settings.General.LastSessionPath);
    }

    [Fact]
    public void Add_SecondPath_AppearsFirst()
    {
        var svc = MakeService(new StubSettingsService());

        svc.Add(@"C:\sessions\first.json");
        svc.Add(@"C:\sessions\second.json");

        Assert.Equal(@"C:\sessions\second.json", svc.GetAll()[0]);
        Assert.Equal(@"C:\sessions\first.json", svc.GetAll()[1]);
    }

    [Fact]
    public void Add_DuplicatePath_BubblesExistingToFront()
    {
        var svc = MakeService(new StubSettingsService());

        svc.Add(@"C:\sessions\demo.json");
        svc.Add(@"C:\sessions\other.json");
        svc.Add(@"C:\sessions\demo.json"); // re-add first path

        Assert.Equal(@"C:\sessions\demo.json", svc.GetAll()[0]);
        Assert.Equal(2, svc.GetAll().Count); // no duplicate entry
    }

    [Fact]
    public void Add_CaseInsensitiveDedupe()
    {
        var svc = MakeService(new StubSettingsService());

        svc.Add(@"C:\Sessions\Demo.json");
        svc.Add(@"c:\sessions\demo.json");

        Assert.Single(svc.GetAll()); // only one entry
    }

    [Fact]
    public void Add_ExceedsMaxItems_OldestDropped()
    {
        var svc = MakeService(new StubSettingsService());

        for (int i = 1; i <= 12; i++)
            svc.Add($@"C:\sessions\file{i}.json");

        Assert.Equal(RecentSessionsService.MaxItems, svc.GetAll().Count);
        // Newest should be first
        Assert.Equal(@"C:\sessions\file12.json", svc.GetAll()[0]);
        // file1 and file2 should have been evicted
        Assert.DoesNotContain(@"C:\sessions\file1.json", svc.GetAll());
        Assert.DoesNotContain(@"C:\sessions\file2.json", svc.GetAll());
    }

    [Fact]
    public void Add_NullOrWhitespace_DoesNothing()
    {
        var svc = MakeService(new StubSettingsService());

        svc.Add(null!);
        svc.Add("   ");

        Assert.Empty(svc.GetAll());
    }

    // ── GetExisting ──────────────────────────────────────────────────────────

    [Fact]
    public void GetExisting_ReturnsOnlyPathsWhereFileExists()
    {
        var existing = new HashSet<string> { @"C:\sessions\exists.json" };
        var svc = MakeService(new StubSettingsService(), existing);

        svc.Add(@"C:\sessions\exists.json");
        svc.Add(@"C:\sessions\missing.json");

        var result = svc.GetExisting();

        Assert.Single(result);
        Assert.Equal(@"C:\sessions\exists.json", result[0]);
    }

    [Fact]
    public void GetExisting_AllMissing_ReturnsEmpty()
    {
        var svc = MakeService(new StubSettingsService(), existingFiles: []);

        svc.Add(@"C:\sessions\gone1.json");
        svc.Add(@"C:\sessions\gone2.json");

        Assert.Empty(svc.GetExisting());
    }

    [Fact]
    public void GetExisting_DoesNotThrow_ForMissingFiles()
    {
        // Even if the predicate throws, GetExisting should not propagate
        var svc = new RecentSessionsService(
            new StubSettingsService(),
            _ => throw new IOException("Disk gone"));

        svc.Add(@"C:\whatever.json");

        // Should return empty rather than throw
        var ex = Record.Exception(() => svc.GetExisting());
        Assert.Null(ex);
        Assert.Empty(svc.GetExisting());
    }

    // ── Exists ───────────────────────────────────────────────────────────────

    [Fact]
    public void Exists_ReturnsTrueForKnownFile()
    {
        var existing = new HashSet<string> { @"C:\f.json" };
        var svc = MakeService(new StubSettingsService(), existing);

        Assert.True(svc.Exists(@"C:\f.json"));
    }

    [Fact]
    public void Exists_ReturnsFalseForUnknownFile()
    {
        var svc = MakeService(new StubSettingsService(), existingFiles: []);

        Assert.False(svc.Exists(@"C:\nope.json"));
    }

    // ── Remove ───────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_ExistingPath_Removed()
    {
        var svc = MakeService(new StubSettingsService());

        svc.Add(@"C:\sessions\demo.json");
        svc.Remove(@"C:\sessions\demo.json");

        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Remove_NonExistentPath_NoOp()
    {
        var svc = MakeService(new StubSettingsService());
        svc.Add(@"C:\sessions\other.json");

        var ex = Record.Exception(() => svc.Remove(@"C:\sessions\nonexistent.json"));

        Assert.Null(ex);
        Assert.Single(svc.GetAll());
    }

    [Fact]
    public void Remove_CaseInsensitive()
    {
        var svc = MakeService(new StubSettingsService());

        svc.Add(@"C:\Sessions\Demo.json");
        svc.Remove(@"c:\sessions\demo.json");

        Assert.Empty(svc.GetAll());
    }

    // ── Clear ────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var svc = MakeService(new StubSettingsService());
        svc.Add(@"C:\a.json");
        svc.Add(@"C:\b.json");

        svc.Clear();

        Assert.Empty(svc.GetAll());
    }

    [Fact]
    public void Clear_WhenAlreadyEmpty_DoesNotThrow()
    {
        var svc = MakeService(new StubSettingsService());

        var ex = Record.Exception(() => svc.Clear());

        Assert.Null(ex);
    }
}
