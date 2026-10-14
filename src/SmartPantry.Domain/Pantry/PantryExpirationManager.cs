using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartPantry.Products;
using Volo.Abp.Guids;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace SmartPantry.Pantry;

public class PantryExpirationManager : DomainService
{
    private readonly IRepository<PantryItem, Guid> _pantryItemRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<PantryItemWarning, Guid> _warningRepository;
    private readonly IGuidGenerator _guidGenerator;

    public PantryExpirationManager(
        IRepository<PantryItem, Guid> pantryItemRepository,
        IRepository<Product, Guid> productRepository,
        IRepository<PantryItemWarning, Guid> warningRepository,
        IGuidGenerator? guidGenerator = null)
    {
        _pantryItemRepository = pantryItemRepository;
        _productRepository = productRepository;
        _warningRepository = warningRepository;
        _guidGenerator = guidGenerator ?? SimpleGuidGenerator.Instance;
    }

    public virtual DateTime GetArgentinaLocalDate()
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
        }
        catch (TimeZoneNotFoundException)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
        }
    }

    public virtual int GetThresholdDays(ItemCategory category)
    {
        return category switch
        {
            ItemCategory.Dairy or ItemCategory.MeatAndFish => 2,
            ItemCategory.Bakery or ItemCategory.FruitsAndVegetables => 3,
            ItemCategory.DryGoods or ItemCategory.CannedGoods => 7,
            _ => PantryWarningConsts.DefaultThresholdDays
        };
    }

    public virtual async Task<int> ProcessExpirationsAsync(
        DateTime? referenceLocalDate = null,
        CancellationToken cancellationToken = default)
    {
        var today = (referenceLocalDate ?? GetArgentinaLocalDate()).Date;

        var items = await _pantryItemRepository.GetListAsync(cancellationToken: cancellationToken);
        if (items.Count == 0)
        {
            return 0;
        }

        var productIds = items.Select(x => x.ProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(x => productIds.Contains(x.Id), cancellationToken: cancellationToken);
        var productsDict = products.ToDictionary(p => p.Id);

        var itemIds = items.Select(x => x.Id).Distinct().ToList();
        var existingWarnings = await _warningRepository.GetListAsync(
            w => itemIds.Contains(w.PantryItemId) && w.WarningType == PantryWarningConsts.ExpirationWarningType,
            cancellationToken: cancellationToken);
        var warningsDict = existingWarnings.ToDictionary(w => w.PantryItemId);

        var activeWarningsCount = 0;

        foreach (var item in items)
        {
            warningsDict.TryGetValue(item.Id, out var existingWarning);

            // Si el ítem no está activo o no tiene fecha de vencimiento:
            if (item.State != PantryItemState.Active || !item.ExpirationDate.HasValue)
            {
                if (existingWarning != null && existingWarning.IsActive)
                {
                    existingWarning.Deactivate();
                    await _warningRepository.UpdateAsync(existingWarning, cancellationToken: cancellationToken);
                }
                continue;
            }

            var expDate = item.ExpirationDate.Value.Date;
            productsDict.TryGetValue(item.ProductId, out var product);
            var category = product?.Category ?? ItemCategory.Undefined;
            var threshold = GetThresholdDays(category);

            if (expDate < today)
            {
                // Vencido
                var message = $"Vencido el {expDate:dd/MM/yyyy}";
                await UpsertWarningAsync(item.Id, existingWarning, message, cancellationToken);
                activeWarningsCount++;
            }
            else if (expDate <= today.AddDays(threshold))
            {
                // Próximo a vencer (dentro de umbral)
                var message = $"Próximo a vencer el {expDate:dd/MM/yyyy}";
                await UpsertWarningAsync(item.Id, existingWarning, message, cancellationToken);
                activeWarningsCount++;
            }
            else
            {
                // Futuro lejano (más allá del umbral)
                if (existingWarning != null && existingWarning.IsActive)
                {
                    existingWarning.Deactivate();
                    await _warningRepository.UpdateAsync(existingWarning, cancellationToken: cancellationToken);
                }
            }
        }

        return activeWarningsCount;
    }

    private async Task UpsertWarningAsync(
        Guid pantryItemId,
        PantryItemWarning? existingWarning,
        string message,
        CancellationToken cancellationToken)
    {
        if (existingWarning != null)
        {
            if (!existingWarning.IsActive || existingWarning.Message != message)
            {
                existingWarning.Reactivate(message);
                await _warningRepository.UpdateAsync(existingWarning, cancellationToken: cancellationToken);
            }
        }
        else
        {
            var warningId = _guidGenerator.Create();
            var newWarning = new PantryItemWarning(
                warningId,
                pantryItemId,
                PantryWarningConsts.ExpirationWarningType,
                message,
                isActive: true);

            await _warningRepository.InsertAsync(newWarning, cancellationToken: cancellationToken);
        }
    }
}
