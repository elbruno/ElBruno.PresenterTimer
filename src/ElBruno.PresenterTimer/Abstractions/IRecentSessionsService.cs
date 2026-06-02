namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Manages the ordered list of recently loaded session file paths stored in
/// <c>AppSettings.General.RecentSessionPaths</c> (PRD §7.16, §10.3).
/// </summary>
public interface IRecentSessionsService
{
    /// <summary>
    /// Adds <paramref name="path"/> to the front of the recent-sessions list.
    /// Deduplicates case-insensitively, caps the list at the configured maximum (10),
    /// and persists settings.  Also updates <c>LastSessionPath</c>.
    /// Never throws.
    /// </summary>
    void Add(string path);

    /// <summary>
    /// Returns all stored recent paths in newest-first order, regardless of whether the
    /// underlying files still exist.
    /// </summary>
    IReadOnlyList<string> GetAll();

    /// <summary>
    /// Returns only the paths whose backing files currently exist on disk.
    /// Callers may use this to populate menus without showing dead entries (PRD §7.16).
    /// Never throws on missing files.
    /// </summary>
    IReadOnlyList<string> GetExisting();

    /// <summary>
    /// Returns <see langword="true"/> if the file at <paramref name="path"/> currently exists.
    /// Uses the same existence check as <see cref="GetExisting"/> so results are consistent.
    /// Never throws.
    /// </summary>
    bool Exists(string path);

    /// <summary>
    /// Removes <paramref name="path"/> from the recent-sessions list and persists settings.
    /// No-op if the path is not in the list.  Never throws.
    /// </summary>
    void Remove(string path);

    /// <summary>
    /// Clears the entire recent-sessions list and persists settings.  Never throws.
    /// </summary>
    void Clear();
}
