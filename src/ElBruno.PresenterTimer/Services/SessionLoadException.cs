namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Thrown by <see cref="SessionLoaderService"/> when a session file cannot be read or parsed.
/// The <see cref="Exception.Message"/> is always human-readable and safe to display in the UI.
/// The optional <see cref="Exception.InnerException"/> carries the original <c>JsonException</c>
/// or <c>IOException</c> for diagnostic purposes.
/// </summary>
public sealed class SessionLoadException : Exception
{
    public SessionLoadException(string message) : base(message) { }

    public SessionLoadException(string message, Exception innerException)
        : base(message, innerException) { }
}
