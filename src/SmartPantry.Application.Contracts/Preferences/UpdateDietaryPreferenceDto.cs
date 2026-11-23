using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;

namespace SmartPantry.Preferences;

public class UpdateDietaryPreferenceDto : IValidatableObject
{
    public List<string> WatchedTerms { get; set; } = new();

    public bool ShowAlertsOnScreen { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (WatchedTerms is null)
        {
            yield break;
        }

        var uniqueNormalizedTerms = new HashSet<string>(StringComparer.Ordinal);
        var totalSerializedLength = 0;

        foreach (var rawTerm in WatchedTerms)
        {
            if (string.IsNullOrWhiteSpace(rawTerm))
            {
                continue;
            }

            var normalized = NormalizeTerm(rawTerm);

            if (normalized.Length < PreferenceConsts.MinTermLength)
            {
                yield return new ValidationResult(
                    $"Cada término vigilado debe tener al menos {PreferenceConsts.MinTermLength} caracteres.",
                    new[] { nameof(WatchedTerms) });
                yield break;
            }

            if (normalized.Length > PreferenceConsts.MaxTermLength)
            {
                yield return new ValidationResult(
                    $"Cada término vigilado no puede superar los {PreferenceConsts.MaxTermLength} caracteres.",
                    new[] { nameof(WatchedTerms) });
                yield break;
            }

            if (uniqueNormalizedTerms.Add(normalized))
            {
                totalSerializedLength += (uniqueNormalizedTerms.Count > 1 ? 1 : 0) + normalized.Length;
            }
        }

        if (uniqueNormalizedTerms.Count > PreferenceConsts.MaxWatchedTermsCount)
        {
            yield return new ValidationResult(
                $"No puede configurar más de {PreferenceConsts.MaxWatchedTermsCount} términos vigilados.",
                new[] { nameof(WatchedTerms) });
        }

        if (totalSerializedLength > PreferenceConsts.MaxSerializedWatchedTermsLength)
        {
            yield return new ValidationResult(
                $"La lista de términos vigilados supera el límite máximo de {PreferenceConsts.MaxSerializedWatchedTermsLength} caracteres.",
                new[] { nameof(WatchedTerms) });
        }
    }

    private static string NormalizeTerm(string value)
    {
        var withoutSeparator = value.Replace(PreferenceConsts.TermSeparator.ToString(), string.Empty);
        var decomposed = withoutSeparator.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasWhitespace = false;

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasWhitespace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(ch);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim().Normalize(NormalizationForm.FormC);
    }
}
