using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Abstractions;

/// <summary>
/// Validates a <see cref="SessionPlan"/> against all rules defined in PRD §7.4.
/// </summary>
public interface ISessionValidationService
{
    /// <summary>
    /// Validates <paramref name="plan"/> and returns a structured <see cref="ValidationResult"/>
    /// containing all error messages. <c>IsValid</c> is <see langword="true"/> when there are no errors.
    /// </summary>
    ValidationResult Validate(SessionPlan plan);
}
