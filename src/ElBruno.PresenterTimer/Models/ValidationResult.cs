namespace ElBruno.PresenterTimer.Models;

/// <summary>
/// Structured result returned by <c>ISessionValidationService.Validate</c>.
/// <para>
/// <c>IsValid</c> is <see langword="true"/> only when <c>Errors</c> is empty.
/// Each error message is human-readable and includes section index/title context
/// matching the PRD §7.4 example style.
/// </para>
/// </summary>
public sealed class ValidationResult
{
    /// <summary>Pre-built success result (no errors).</summary>
    public static ValidationResult Success { get; } = new(true, []);

    public bool IsValid { get; }
    public IReadOnlyList<string> Errors { get; }

    public ValidationResult(bool isValid, IReadOnlyList<string> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    /// <summary>Creates a failed result carrying one or more error messages.</summary>
    public static ValidationResult Failure(IReadOnlyList<string> errors)
        => new(false, errors);
}
