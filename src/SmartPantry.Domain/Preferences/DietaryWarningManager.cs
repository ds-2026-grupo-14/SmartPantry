using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartPantry.Pantry;
using SmartPantry.Products;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace SmartPantry.Preferences;

public sealed record DietaryEvaluationResult(
    DietaryVerificationStatus Status,
    IReadOnlyList<string> MatchedTerms,
    string? WarningMessage = null);

public class DietaryWarningManager : DomainService
{
    public const string UnverifiableWarningMessage =
        "No verificable: faltan datos de ingredientes o alérgenos para contrastar con sus preferencias.";

    private const string MatchWarningPrefix =
        "Advertencia informativa: coincidencia detectada con sus preferencias: ";

    private readonly IRepository<PantryItemWarning, Guid> _warningRepository;
    private readonly IGuidGenerator _guidGenerator;

    public DietaryWarningManager(
        IRepository<PantryItemWarning, Guid> warningRepository,
        IGuidGenerator? guidGenerator = null)
    {
        _warningRepository = warningRepository;
        _guidGenerator = guidGenerator ?? SimpleGuidGenerator.Instance;
    }

    public virtual DietaryEvaluationResult Evaluate(
        string? ingredients,
        string? allergens,
        UserDietaryPreference? preference)
    {
        if (preference is null || !preference.ShowAlertsOnScreen)
        {
            return new DietaryEvaluationResult(
                DietaryVerificationStatus.NotConfigured,
                Array.Empty<string>());
        }

        var watchedTerms = preference.GetWatchedTermsList();
        if (watchedTerms.Count == 0)
        {
            return new DietaryEvaluationResult(
                DietaryVerificationStatus.NotConfigured,
                Array.Empty<string>());
        }

        var normalizedIngredients = NormalizeForComparison(ingredients);
        var normalizedAllergens = NormalizeForComparison(allergens);

        var matches = watchedTerms
            .Where(term =>
                (!string.IsNullOrEmpty(normalizedAllergens) && normalizedAllergens.Contains(term, StringComparison.Ordinal)) ||
                (!string.IsNullOrEmpty(normalizedIngredients) && normalizedIngredients.Contains(term, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (matches.Count > 0)
        {
            return new DietaryEvaluationResult(
                DietaryVerificationStatus.Warning,
                matches,
                BuildWarningMessage(matches));
        }

        // BR-002: Ausencia de ingredientes O alérgenos jamás equivale a ausencia de riesgo
        if (string.IsNullOrEmpty(normalizedIngredients) || string.IsNullOrEmpty(normalizedAllergens))
        {
            return new DietaryEvaluationResult(
                DietaryVerificationStatus.Unverifiable,
                Array.Empty<string>(),
                UnverifiableWarningMessage);
        }

        return new DietaryEvaluationResult(
            DietaryVerificationStatus.Compatible,
            Array.Empty<string>());
    }

    public virtual async Task<DietaryEvaluationResult> SyncItemDietaryWarningAsync(
        PantryItem item,
        Product product,
        UserDietaryPreference? preference,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(item, nameof(item));
        Check.NotNull(product, nameof(product));

        var evaluation = Evaluate(product.Ingredients, product.Allergens, preference);

        var existingWarning = await _warningRepository.FindAsync(
            w => w.PantryItemId == item.Id && w.WarningType == PreferenceConsts.DietaryPreferenceWarningType,
            cancellationToken: cancellationToken);

        await ApplyWarningStateAsync(item, existingWarning, evaluation, cancellationToken);

        return evaluation;
    }

    public virtual async Task<int> SyncItemsDietaryWarningsAsync(
        IReadOnlyCollection<PantryItem> items,
        IReadOnlyDictionary<Guid, Product> productsById,
        UserDietaryPreference? preference,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(items, nameof(items));
        Check.NotNull(productsById, nameof(productsById));

        if (items.Count == 0)
        {
            return 0;
        }

        var itemIds = items.Select(x => x.Id).Distinct().ToList();
        var existingWarnings = await _warningRepository.GetListAsync(
            w => itemIds.Contains(w.PantryItemId) && w.WarningType == PreferenceConsts.DietaryPreferenceWarningType,
            cancellationToken: cancellationToken);
        var warningsByItemId = existingWarnings.ToDictionary(w => w.PantryItemId);

        var activeWarningsCount = 0;

        foreach (var item in items)
        {
            productsById.TryGetValue(item.ProductId, out var product);
            warningsByItemId.TryGetValue(item.Id, out var existingWarning);

            var evaluation = Evaluate(product?.Ingredients, product?.Allergens, preference);
            var isActiveAfterSync = await ApplyWarningStateAsync(item, existingWarning, evaluation, cancellationToken);

            if (isActiveAfterSync)
            {
                activeWarningsCount++;
            }
        }

        return activeWarningsCount;
    }

    public static string NormalizeForComparison(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
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

    private async Task<bool> ApplyWarningStateAsync(
        PantryItem item,
        PantryItemWarning? existingWarning,
        DietaryEvaluationResult evaluation,
        CancellationToken cancellationToken)
    {
        var shouldHaveActiveWarning =
            item.State == PantryItemState.Active &&
            (evaluation.Status == DietaryVerificationStatus.Warning ||
             evaluation.Status == DietaryVerificationStatus.Unverifiable);

        if (!shouldHaveActiveWarning)
        {
            if (existingWarning is { IsActive: true })
            {
                existingWarning.Deactivate();
                await _warningRepository.UpdateAsync(existingWarning, cancellationToken: cancellationToken);
            }

            return false;
        }

        var message = evaluation.WarningMessage ?? UnverifiableWarningMessage;

        if (existingWarning is not null)
        {
            if (!existingWarning.IsActive || !string.Equals(existingWarning.Message, message, StringComparison.Ordinal))
            {
                existingWarning.Reactivate(message);
                await _warningRepository.UpdateAsync(existingWarning, cancellationToken: cancellationToken);
            }
        }
        else
        {
            var newWarning = new PantryItemWarning(
                _guidGenerator.Create(),
                item.Id,
                PreferenceConsts.DietaryPreferenceWarningType,
                message,
                isActive: true);

            await _warningRepository.InsertAsync(newWarning, cancellationToken: cancellationToken);
        }

        return true;
    }

    private static string BuildWarningMessage(IReadOnlyList<string> matches)
    {
        var rawMessage = MatchWarningPrefix + string.Join(", ", matches);
        if (rawMessage.Length <= PantryWarningConsts.MaxMessageLength)
        {
            return rawMessage;
        }

        return rawMessage[..(PantryWarningConsts.MaxMessageLength - 3)] + "...";
    }
}
