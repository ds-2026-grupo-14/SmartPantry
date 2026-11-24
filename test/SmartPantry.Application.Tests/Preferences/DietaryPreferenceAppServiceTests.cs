using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using SmartPantry.Pantry;
using SmartPantry.Products;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace SmartPantry.Preferences;

public abstract class DietaryPreferenceAppServiceTests<TStartupModule> : SmartPantryApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected readonly IDietaryPreferenceAppService DietaryPreferenceAppService;
    protected readonly IRepository<UserDietaryPreference, Guid> PreferenceRepository;
    protected readonly IRepository<PantryItem, Guid> PantryItemRepository;
    protected readonly IRepository<PantryItemWarning, Guid> WarningRepository;
    protected readonly IRepository<Product, Guid> ProductRepository;
    protected readonly IRepository<IdentityUser, Guid> IdentityUserRepository;
    protected readonly ICurrentPrincipalAccessor CurrentPrincipalAccessor;

    protected DietaryPreferenceAppServiceTests()
    {
        DietaryPreferenceAppService = GetRequiredService<IDietaryPreferenceAppService>();
        PreferenceRepository = GetRequiredService<IRepository<UserDietaryPreference, Guid>>();
        PantryItemRepository = GetRequiredService<IRepository<PantryItem, Guid>>();
        WarningRepository = GetRequiredService<IRepository<PantryItemWarning, Guid>>();
        ProductRepository = GetRequiredService<IRepository<Product, Guid>>();
        IdentityUserRepository = GetRequiredService<IRepository<IdentityUser, Guid>>();
        CurrentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    protected async Task<IdentityUser> CreateTestUserAsync(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var userName = $"{prefix}_{suffix}";

        return await WithUnitOfWorkAsync(async () =>
        {
            var user = new IdentityUser(Guid.NewGuid(), userName, $"{userName}@test.local");
            return await IdentityUserRepository.InsertAsync(user, autoSave: true);
        });
    }

    protected IDisposable ChangeCurrentUser(IdentityUser user)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, user.Id.ToString()),
            new Claim(AbpClaimTypes.UserName, user.UserName),
            new Claim(AbpClaimTypes.Email, user.Email)
        });

        return CurrentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    protected async Task<Product> CreateProductAsync(
        string name,
        string? ingredients,
        string? allergens)
    {
        var barcode = $"779{Random.Shared.Next(10000000, 99999999)}{Random.Shared.Next(10, 99)}";

        return await WithUnitOfWorkAsync(async () =>
        {
            var product = new Product(
                Guid.NewGuid(),
                barcode,
                name,
                ItemCategory.Snacks,
                brand: "Marca Test",
                ingredients: ingredients,
                allergens: allergens);

            return await ProductRepository.InsertAsync(product, autoSave: true);
        });
    }

    [Fact]
    public async Task Should_Return_Default_Preferences_When_Not_Configured_Yet()
    {
        var user = await CreateTestUserAsync("default_pref");

        using (ChangeCurrentUser(user))
        {
            var dto = await DietaryPreferenceAppService.GetMyPreferencesAsync();

            dto.ShouldNotBeNull();
            dto.UserId.ShouldBe(user.Id);
            dto.WatchedTerms.ShouldBeEmpty();
            dto.ShowAlertsOnScreen.ShouldBeTrue();
            dto.LastModificationTime.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Should_Create_And_Update_My_Preferences_Normalizing_And_Deduplicating_Terms()
    {
        var user = await CreateTestUserAsync("crud_pref");

        using (ChangeCurrentUser(user))
        {
            var created = await DietaryPreferenceAppService.UpdateMyPreferencesAsync(new UpdateDietaryPreferenceDto
            {
                WatchedTerms = new List<string> { "Gluten", "Lactosa", "  Maní ", "mani", "GLUTEN" },
                ShowAlertsOnScreen = true
            });

            created.UserId.ShouldBe(user.Id);
            created.WatchedTerms.ShouldBe(new List<string> { "gluten", "lactosa", "mani" });
            created.ShowAlertsOnScreen.ShouldBeTrue();

            var loaded = await DietaryPreferenceAppService.GetMyPreferencesAsync();
            loaded.WatchedTerms.ShouldBe(new List<string> { "gluten", "lactosa", "mani" });
            loaded.ShowAlertsOnScreen.ShouldBeTrue();

            // Segunda actualización sobre el mismo registro 1:1
            var updated = await DietaryPreferenceAppService.UpdateMyPreferencesAsync(new UpdateDietaryPreferenceDto
            {
                WatchedTerms = new List<string> { "Soja" },
                ShowAlertsOnScreen = false
            });

            updated.WatchedTerms.ShouldBe(new List<string> { "soja" });
            updated.ShowAlertsOnScreen.ShouldBeFalse();

            var rowsForUser = await WithUnitOfWorkAsync(async () =>
                await PreferenceRepository.GetListAsync(x => x.UserId == user.Id));
            rowsForUser.Count.ShouldBe(1);
        }
    }

    [Fact]
    public async Task Should_Isolate_Preferences_Between_Users_Anti_IDOR()
    {
        var userA = await CreateTestUserAsync("user_a");
        var userB = await CreateTestUserAsync("user_b");

        // 1. Usuario A configura sus preferencias
        using (ChangeCurrentUser(userA))
        {
            await DietaryPreferenceAppService.UpdateMyPreferencesAsync(new UpdateDietaryPreferenceDto
            {
                WatchedTerms = new List<string> { "gluten", "mani" },
                ShowAlertsOnScreen = true
            });
        }

        // 2. Usuario B no debe ver preferencias de A y configura las suyas
        using (ChangeCurrentUser(userB))
        {
            var initialB = await DietaryPreferenceAppService.GetMyPreferencesAsync();
            initialB.UserId.ShouldBe(userB.Id);
            initialB.WatchedTerms.ShouldBeEmpty();
            initialB.ShowAlertsOnScreen.ShouldBeTrue();

            await DietaryPreferenceAppService.UpdateMyPreferencesAsync(new UpdateDietaryPreferenceDto
            {
                WatchedTerms = new List<string> { "soja" },
                ShowAlertsOnScreen = false
            });
        }

        // 3. Usuario A mantiene sus preferencias intactas
        using (ChangeCurrentUser(userA))
        {
            var reloadedA = await DietaryPreferenceAppService.GetMyPreferencesAsync();
            reloadedA.UserId.ShouldBe(userA.Id);
            reloadedA.WatchedTerms.ShouldBe(new List<string> { "gluten", "mani" });
            reloadedA.ShowAlertsOnScreen.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Should_Evaluate_Product_Across_All_Four_DietaryVerificationStatus_States()
    {
        var user = await CreateTestUserAsync("eval_user");

        using (ChangeCurrentUser(user))
        {
            // 1. Sin preferencias guardadas -> NotConfigured
            var notConfigured = await DietaryPreferenceAppService.EvaluateProductAsync(new EvaluateProductDietaryDto
            {
                Ingredients = "Harina de trigo",
                Allergens = "en:gluten"
            });
            notConfigured.VerificationStatus.ShouldBe(DietaryVerificationStatus.NotConfigured);
            notConfigured.MatchedTerms.ShouldBeEmpty();

            // Configurar términos vigilados
            await DietaryPreferenceAppService.UpdateMyPreferencesAsync(new UpdateDietaryPreferenceDto
            {
                WatchedTerms = new List<string> { "gluten", "Maní" },
                ShowAlertsOnScreen = true
            });

            // 2. Coincidencia detectada -> Warning (con mensaje informativo BR-001)
            var warning = await DietaryPreferenceAppService.EvaluateProductAsync(new EvaluateProductDietaryDto
            {
                Ingredients = "Harina de trigo, pasta de MANI tostado",
                Allergens = "en:gluten"
            });
            warning.VerificationStatus.ShouldBe(DietaryVerificationStatus.Warning);
            warning.MatchedTerms.ShouldBe(new List<string> { "gluten", "mani" });
            warning.InformationalMessage.ShouldNotBeNullOrWhiteSpace();
            warning.InformationalMessage!.ShouldContain("Advertencia informativa");
            warning.InformationalMessage!.ShouldContain("no constituye diagnóstico médico");

            // 3. Datos faltantes sin coincidencia -> Unverifiable (BR-002)
            var unverifiable = await DietaryPreferenceAppService.EvaluateProductAsync(new EvaluateProductDietaryDto
            {
                Ingredients = "Agua mineral, avena",
                Allergens = null
            });
            unverifiable.VerificationStatus.ShouldBe(DietaryVerificationStatus.Unverifiable);
            unverifiable.MatchedTerms.ShouldBeEmpty();
            unverifiable.InformationalMessage.ShouldNotBeNullOrWhiteSpace();
            unverifiable.InformationalMessage!.ShouldContain("No verificable");

            // 4. Ambos campos completos y sin coincidencias -> Compatible
            var compatible = await DietaryPreferenceAppService.EvaluateProductAsync(new EvaluateProductDietaryDto
            {
                Ingredients = "Agua mineral, jugo de manzana",
                Allergens = "none"
            });
            compatible.VerificationStatus.ShouldBe(DietaryVerificationStatus.Compatible);
            compatible.MatchedTerms.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Should_Recalculate_PantryItemWarnings_Idempotently_And_Isolate_Other_Users()
    {
        var userA = await CreateTestUserAsync("sync_a");
        var userB = await CreateTestUserAsync("sync_b");

        var matchingProduct = await CreateProductAsync(
            "Galletitas con Gluten",
            ingredients: "Harina de trigo, azúcar",
            allergens: "en:gluten");
        var incompleteProduct = await CreateProductAsync(
            "Bebida sin Alérgenos Declarados",
            ingredients: "Agua, jugo de naranja",
            allergens: null);
        var safeProduct = await CreateProductAsync(
            "Agua Mineral",
            ingredients: "Agua mineral natural",
            allergens: "none");

        var itemAMatch = await WithUnitOfWorkAsync(async () =>
            await PantryItemRepository.InsertAsync(
                new PantryItem(Guid.NewGuid(), userA.Id, matchingProduct.Id, state: PantryItemState.Active),
                autoSave: true));
        var itemAIncomplete = await WithUnitOfWorkAsync(async () =>
            await PantryItemRepository.InsertAsync(
                new PantryItem(Guid.NewGuid(), userA.Id, incompleteProduct.Id, state: PantryItemState.Active),
                autoSave: true));
        var itemASafe = await WithUnitOfWorkAsync(async () =>
            await PantryItemRepository.InsertAsync(
                new PantryItem(Guid.NewGuid(), userA.Id, safeProduct.Id, state: PantryItemState.Active),
                autoSave: true));
        var itemAConsumed = await WithUnitOfWorkAsync(async () =>
            await PantryItemRepository.InsertAsync(
                new PantryItem(Guid.NewGuid(), userA.Id, matchingProduct.Id, state: PantryItemState.Consumed),
                autoSave: true));
        var itemBMatch = await WithUnitOfWorkAsync(async () =>
            await PantryItemRepository.InsertAsync(
                new PantryItem(Guid.NewGuid(), userB.Id, matchingProduct.Id, state: PantryItemState.Active),
                autoSave: true));

        using (ChangeCurrentUser(userA))
        {
            // 1. Primera actualización de preferencias -> crea 2 advertencias activas solo para User A
            await DietaryPreferenceAppService.UpdateMyPreferencesAsync(new UpdateDietaryPreferenceDto
            {
                WatchedTerms = new List<string> { "gluten" },
                ShowAlertsOnScreen = true
            });

            var warningsAfterFirstRun = await WithUnitOfWorkAsync(async () =>
                await WarningRepository.GetListAsync(w => w.WarningType == PreferenceConsts.DietaryPreferenceWarningType));

            var userAWarnings = warningsAfterFirstRun
                .Where(w => w.PantryItemId == itemAMatch.Id ||
                            w.PantryItemId == itemAIncomplete.Id ||
                            w.PantryItemId == itemASafe.Id ||
                            w.PantryItemId == itemAConsumed.Id)
                .ToList();

            userAWarnings.Count.ShouldBe(2);
            var matchWarning = userAWarnings.Single(w => w.PantryItemId == itemAMatch.Id);
            matchWarning.IsActive.ShouldBeTrue();
            matchWarning.Message.ShouldContain("gluten");

            var unverifiableWarning = userAWarnings.Single(w => w.PantryItemId == itemAIncomplete.Id);
            unverifiableWarning.IsActive.ShouldBeTrue();
            unverifiableWarning.Message.ShouldBe(DietaryWarningManager.UnverifiableWarningMessage);

            // Aislamiento estricto: el ítem de Usuario B no debe recibir advertencias al mutar preferencias de A
            warningsAfterFirstRun.ShouldNotContain(w => w.PantryItemId == itemBMatch.Id);

            // 2. Segunda actualización idéntica -> idempotencia exacta (sin duplicados, mismos Ids)
            await DietaryPreferenceAppService.UpdateMyPreferencesAsync(new UpdateDietaryPreferenceDto
            {
                WatchedTerms = new List<string> { "Gluten" },
                ShowAlertsOnScreen = true
            });

            var warningsAfterSecondRun = await WithUnitOfWorkAsync(async () =>
                await WarningRepository.GetListAsync(w =>
                    (w.PantryItemId == itemAMatch.Id || w.PantryItemId == itemAIncomplete.Id) &&
                    w.WarningType == PreferenceConsts.DietaryPreferenceWarningType));

            warningsAfterSecondRun.Count.ShouldBe(2);
            warningsAfterSecondRun.Single(w => w.PantryItemId == itemAMatch.Id).Id.ShouldBe(matchWarning.Id);
            warningsAfterSecondRun.Single(w => w.PantryItemId == itemAIncomplete.Id).Id.ShouldBe(unverifiableWarning.Id);

            // 3. Desactivar alertas en pantalla -> desactiva las filas sin eliminarlas
            await DietaryPreferenceAppService.UpdateMyPreferencesAsync(new UpdateDietaryPreferenceDto
            {
                WatchedTerms = new List<string> { "gluten" },
                ShowAlertsOnScreen = false
            });

            var warningsAfterDisable = await WithUnitOfWorkAsync(async () =>
                await WarningRepository.GetListAsync(w =>
                    (w.PantryItemId == itemAMatch.Id || w.PantryItemId == itemAIncomplete.Id) &&
                    w.WarningType == PreferenceConsts.DietaryPreferenceWarningType));

            warningsAfterDisable.Count.ShouldBe(2);
            warningsAfterDisable.ShouldAllBe(w => !w.IsActive);
        }
    }

    [Fact]
    public async Task Should_Reject_Invalid_Terms_With_ValidationException()
    {
        var user = await CreateTestUserAsync("invalid_pref");

        using (ChangeCurrentUser(user))
        {
            // Término de 1 carácter (< MinTermLength = 2)
            await Should.ThrowAsync<AbpValidationException>(async () =>
            {
                await DietaryPreferenceAppService.UpdateMyPreferencesAsync(new UpdateDietaryPreferenceDto
                {
                    WatchedTerms = new List<string> { "a" },
                    ShowAlertsOnScreen = true
                });
            });

            // Más de 30 términos únicos (> MaxWatchedTermsCount = 30)
            var tooManyTerms = Enumerable.Range(1, PreferenceConsts.MaxWatchedTermsCount + 1)
                .Select(i => $"alergeno{i:D2}")
                .ToList();

            await Should.ThrowAsync<AbpValidationException>(async () =>
            {
                await DietaryPreferenceAppService.UpdateMyPreferencesAsync(new UpdateDietaryPreferenceDto
                {
                    WatchedTerms = tooManyTerms,
                    ShowAlertsOnScreen = true
                });
            });
        }
    }
}
