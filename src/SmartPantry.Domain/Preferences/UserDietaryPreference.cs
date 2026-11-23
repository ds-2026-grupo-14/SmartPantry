using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace SmartPantry.Preferences;

public class UserDietaryPreference : AuditedAggregateRoot<Guid>
{
    public Guid UserId { get; private set; }

    public string WatchedTerms { get; private set; } = string.Empty;

    public bool ShowAlertsOnScreen { get; private set; } = true;

    // Constructor sin parámetros requerido por Entity Framework Core
    protected UserDietaryPreference()
    {
    }

    public UserDietaryPreference(
        Guid id,
        Guid userId,
        IEnumerable<string>? watchedTerms = null,
        bool showAlertsOnScreen = true) : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new BusinessException(
                "SmartPantry:UserDietaryPreference:InvalidUserId",
                "El UserId no puede ser vacío.");
        }

        UserId = userId;
        SetWatchedTerms(watchedTerms);
        SetShowAlertsOnScreen(showAlertsOnScreen);
    }

    public IReadOnlyList<string> GetWatchedTermsList()
    {
        if (string.IsNullOrWhiteSpace(WatchedTerms))
        {
            return Array.Empty<string>();
        }

        return WatchedTerms
            .Split(PreferenceConsts.TermSeparator, StringSplitOptions.RemoveEmptyEntries);
    }

    public void SetWatchedTerms(IEnumerable<string>? terms)
    {
        if (terms is null)
        {
            WatchedTerms = string.Empty;
            return;
        }

        var normalizedTerms = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawTerm in terms)
        {
            if (string.IsNullOrWhiteSpace(rawTerm))
            {
                continue;
            }

            var normalized = NormalizeTerm(rawTerm);

            if (normalized.Length < PreferenceConsts.MinTermLength)
            {
                throw new BusinessException(
                    "SmartPantry:UserDietaryPreference:TermTooShort",
                    $"Cada término vigilado debe tener al menos {PreferenceConsts.MinTermLength} caracteres.");
            }

            if (normalized.Length > PreferenceConsts.MaxTermLength)
            {
                throw new BusinessException(
                    "SmartPantry:UserDietaryPreference:TermTooLong",
                    $"Cada término vigilado no puede superar los {PreferenceConsts.MaxTermLength} caracteres.");
            }

            if (seen.Add(normalized))
            {
                normalizedTerms.Add(normalized);
            }
        }

        if (normalizedTerms.Count > PreferenceConsts.MaxWatchedTermsCount)
        {
            throw new BusinessException(
                "SmartPantry:UserDietaryPreference:TooManyWatchedTerms",
                $"No puede configurar más de {PreferenceConsts.MaxWatchedTermsCount} términos vigilados.");
        }

        var serialized = string.Join(PreferenceConsts.TermSeparator, normalizedTerms);
        if (serialized.Length > PreferenceConsts.MaxSerializedWatchedTermsLength)
        {
            throw new BusinessException(
                "SmartPantry:UserDietaryPreference:WatchedTermsOverflow",
                $"La lista de términos vigilados supera el límite máximo de {PreferenceConsts.MaxSerializedWatchedTermsLength} caracteres.");
        }

        WatchedTerms = serialized;
    }

    public void SetShowAlertsOnScreen(bool show)
    {
        ShowAlertsOnScreen = show;
    }

    public static string NormalizeTerm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

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
