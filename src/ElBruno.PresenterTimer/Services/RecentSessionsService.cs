using System.IO;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Manages the recent-sessions list stored in <see cref="GeneralSettings.RecentSessionPaths"/>
/// via <see cref="ISettingsService"/> (PRD §7.16, §10.3).
/// </summary>
/// <remarks>
/// Testability seam: inject a <paramref name="fileExists"/> predicate to avoid real disk I/O in
/// unit tests.  The production default is <see cref="File.Exists"/>.
/// </remarks>
public sealed class RecentSessionsService : IRecentSessionsService
{
    internal const int MaxItems = 10;

    private readonly ISettingsService _settingsService;
    private readonly Func<string, bool> _fileExists;

    /// <param name="settingsService">Used to read/write <see cref="GeneralSettings"/>.</param>
    /// <param name="fileExists">
    /// Optional override for the file-existence check.
    /// Defaults to <see cref="File.Exists"/> when <see langword="null"/>.
    /// </param>
    public RecentSessionsService(ISettingsService settingsService, Func<string, bool>? fileExists = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _fileExists = fileExists ?? File.Exists;
    }

    /// <inheritdoc/>
    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var list = _settingsService.Settings.General.RecentSessionPaths;

        // Deduplicate: remove any existing entry for the same path (case-insensitive on Windows)
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));

        // Insert most-recent-first
        list.Insert(0, path);

        // Cap at maximum
        while (list.Count > MaxItems)
            list.RemoveAt(list.Count - 1);

        // Keep LastSessionPath in sync
        _settingsService.Settings.General.LastSessionPath = path;

        _settingsService.Save();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAll() =>
        _settingsService.Settings.General.RecentSessionPaths.AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyList<string> GetExisting()
    {
        // Never throw on missing files — filter them silently (PRD §10.3)
        return _settingsService.Settings.General.RecentSessionPaths
            .Where(p => SafeFileExists(p))
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc/>
    public bool Exists(string path) => SafeFileExists(path);

    /// <inheritdoc/>
    public void Remove(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        _settingsService.Settings.General.RecentSessionPaths
            .RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));

        _settingsService.Save();
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _settingsService.Settings.General.RecentSessionPaths.Clear();
        _settingsService.Save();
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private bool SafeFileExists(string path)
    {
        try { return _fileExists(path); }
        catch { return false; } // defensive: never crash on bad paths (PRD §10.3)
    }
}
