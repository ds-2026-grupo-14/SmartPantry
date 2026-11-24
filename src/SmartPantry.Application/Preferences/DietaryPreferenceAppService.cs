using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SmartPantry.Pantry;
using SmartPantry.Products;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace SmartPantry.Preferences;

[Authorize]
public class DietaryPreferenceAppService : SmartPantryAppService, IDietaryPreferenceAppService
{
    private readonly IRepository<UserDietaryPreference, Guid> _preferenceRepository;
    private readonly IRepository<PantryItem, Guid> _pantryItemRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly DietaryWarningManager _dietaryWarningManager;

    public DietaryPreferenceAppService(
        IRepository<UserDietaryPreference, Guid> preferenceRepository,
        IRepository<PantryItem, Guid> pantryItemRepository,
        IRepository<Product, Guid> productRepository,
        DietaryWarningManager dietaryWarningManager)
    {
        _preferenceRepository = preferenceRepository;
        _pantryItemRepository = pantryItemRepository;
        _productRepository = productRepository;
        _dietaryWarningManager = dietaryWarningManager;
    }

    public async Task<DietaryPreferenceDto> GetMyPreferencesAsync()
    {
        var currentUserId = CurrentUser.GetId();
        var entity = await _preferenceRepository.FindAsync(x => x.UserId == currentUserId);

        if (entity is null)
        {
            return new DietaryPreferenceDto
            {
                UserId = currentUserId,
                WatchedTerms = new List<string>(),
                ShowAlertsOnScreen = true,
                LastModificationTime = null
            };
        }

        return MapToDto(entity);
    }

    public async Task<DietaryPreferenceDto> UpdateMyPreferencesAsync(UpdateDietaryPreferenceDto input)
    {
        var currentUserId = CurrentUser.GetId();
        var entity = await _preferenceRepository.FindAsync(x => x.UserId == currentUserId);

        if (entity is null)
        {
            entity = new UserDietaryPreference(
                GuidGenerator.Create(),
                currentUserId,
                input.WatchedTerms,
                input.ShowAlertsOnScreen);

            await _preferenceRepository.InsertAsync(entity, autoSave: true);
        }
        else
        {
            entity.SetWatchedTerms(input.WatchedTerms);
            entity.SetShowAlertsOnScreen(input.ShowAlertsOnScreen);
            await _preferenceRepository.UpdateAsync(entity, autoSave: true);
        }

        // BR-010: Recalcular de forma idempotente las advertencias "DietaryPreference" de la despensa del usuario
        var userItems = await _pantryItemRepository.GetListAsync(x => x.UserId == currentUserId);
        if (userItems.Count > 0)
        {
            var productIds = userItems.Select(x => x.ProductId).Distinct().ToList();
            var products = await _productRepository.GetListAsync(x => productIds.Contains(x.Id));
            var productsById = products.ToDictionary(p => p.Id);

            await _dietaryWarningManager.SyncItemsDietaryWarningsAsync(userItems, productsById, entity);
        }

        return MapToDto(entity);
    }

    public async Task<DietaryEvaluationDto> EvaluateProductAsync(EvaluateProductDietaryDto input)
    {
        var currentUserId = CurrentUser.GetId();
        var preference = await _preferenceRepository.FindAsync(x => x.UserId == currentUserId);

        return BuildEvaluationDto(
            _dietaryWarningManager.Evaluate(input.Ingredients, input.Allergens, preference));
    }

    public static DietaryEvaluationDto BuildEvaluationDto(DietaryEvaluationResult result)
    {
        var message = result.Status switch
        {
            DietaryVerificationStatus.Warning =>
                $"Advertencia informativa: se detectaron coincidencias con sus preferencias configuradas ({string.Join(", ", result.MatchedTerms)}). Esta información proviene de Open Food Facts y no constituye diagnóstico médico.",
            DietaryVerificationStatus.Unverifiable =>
                "No verificable: el proveedor externo no informa la totalidad de ingredientes o alérgenos para descartar coincidencias con sus preferencias.",
            DietaryVerificationStatus.Compatible =>
                "Sin coincidencias detectadas con sus preferencias configuradas. Esta información es estrictamente informativa y no constituye diagnóstico médico.",
            _ => null
        };

        return new DietaryEvaluationDto
        {
            VerificationStatus = result.Status,
            MatchedTerms = result.MatchedTerms.ToList(),
            InformationalMessage = message
        };
    }

    private static DietaryPreferenceDto MapToDto(UserDietaryPreference entity) => new()
    {
        UserId = entity.UserId,
        WatchedTerms = entity.GetWatchedTermsList().ToList(),
        ShowAlertsOnScreen = entity.ShowAlertsOnScreen,
        LastModificationTime = entity.LastModificationTime
    };
}
