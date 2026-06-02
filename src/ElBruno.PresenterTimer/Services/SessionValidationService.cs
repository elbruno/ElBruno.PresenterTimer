using System.Text.RegularExpressions;
using ElBruno.PresenterTimer.Abstractions;
using ElBruno.PresenterTimer.Models;

namespace ElBruno.PresenterTimer.Services;

/// <summary>
/// Validates a <see cref="SessionPlan"/> against all rules defined in PRD §7.4.
/// Returns a <see cref="ValidationResult"/> with human-readable error messages;
/// never throws on a semantically invalid plan.
/// </summary>
public sealed class SessionValidationService : ISessionValidationService
{
    // Accepts #RGB and #RRGGBB only (case-insensitive)
    private static readonly Regex HexColorRegex =
        new(@"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$", RegexOptions.Compiled);

    /// <inheritdoc/>
    public ValidationResult Validate(SessionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var errors = new List<string>();

        // Rule: session title must not be empty
        if (string.IsNullOrWhiteSpace(plan.Title))
            errors.Add("Session title must not be empty.");

        // Rule: sections array must exist and have at least one entry
        if (plan.Sections is null || plan.Sections.Count == 0)
        {
            errors.Add("Session must contain at least one section.");
            return ValidationResult.Failure(errors);
        }

        // Validate each section
        for (int i = 0; i < plan.Sections.Count; i++)
        {
            var section = plan.Sections[i];
            int number = i + 1;

            // Build a label matching PRD §7.4 example style
            string label = string.IsNullOrWhiteSpace(section.Title)
                ? $"Section {number}"
                : $"Section {number}, \"{section.Title.Trim()}\"";

            // Rule: every section must have a title
            if (string.IsNullOrWhiteSpace(section.Title))
                errors.Add($"Section {number} is missing a title.");

            // Rule: every section must have a valid duration > zero
            if (section.Duration <= TimeSpan.Zero)
                errors.Add($"{label} has a duration of {section.Duration:hh\\:mm\\:ss}, which must be greater than zero.");

            // Rule: optional color must be a valid hex value (#RGB or #RRGGBB)
            if (section.Color is not null && !HexColorRegex.IsMatch(section.Color))
                errors.Add($"{label} has an invalid color \"{section.Color}\". Colors must be in #RGB or #RRGGBB format.");

            // Rule: warningAt must be < section duration (when both are defined and duration is positive)
            if (section.WarningAt.HasValue && section.Duration > TimeSpan.Zero
                && section.WarningAt.Value >= section.Duration)
            {
                errors.Add(
                    $"{label} has a warning time of {section.WarningAt.Value:hh\\:mm\\:ss} " +
                    $"but the section duration is only {section.Duration:hh\\:mm\\:ss}.");
            }
        }

        // Rule: total duration must be > zero
        var total = plan.Sections.Aggregate(TimeSpan.Zero, (acc, s) => acc + s.Duration);
        if (total <= TimeSpan.Zero)
            errors.Add("Total session duration must be greater than zero.");

        return errors.Count == 0
            ? ValidationResult.Success
            : ValidationResult.Failure(errors);
    }
}
