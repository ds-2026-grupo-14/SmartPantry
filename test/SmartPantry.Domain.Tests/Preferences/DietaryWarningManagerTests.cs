using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using SmartPantry.Pantry;
using SmartPantry.Products;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace SmartPantry.Preferences;

public class DietaryWarningManagerTests
{
    private readonly IRepository<PantryItemWarning, Guid> _warningRepository;
    private readonly DietaryWarningManager _manager;
    private readonly List<PantryItemWarning> _warningsDb = new();
    private readonly Guid _userId = Guid.NewGuid();

    public DietaryWarningManagerTests()
    {
        _warningRepository = Substitute.For<IRepository<PantryItemWarning, Guid>>();

        _warningRepository.FindAsync(
                Arg.Any<Expression<Func<PantryItemWarning, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.Arg<Expression<Func<PantryItemWarning, bool>>>().Compile();
                return Task.FromResult(_warningsDb.FirstOrDefault(predicate));
            });

        _warningRepository.GetListAsync(
                Arg.Any<Expression<Func<PantryItemWarning, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.Arg<Expression<Func<PantryItemWarning, bool>>>().Compile();
                return Task.FromResult(_warningsDb.Where(predicate).ToList());
            });

        _warningRepository.InsertAsync(
                Arg.Any<PantryItemWarning>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var entity = callInfo.Arg<PantryItemWarning>();
                _warningsDb.Add(entity);
                return Task.FromResult(entity);
            });

        _warningRepository.UpdateAsync(
                Arg.Any<PantryItemWarning>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var entity = callInfo.Arg<PantryItemWarning>();
                var index = _warningsDb.FindIndex(w => w.Id == entity.Id);
                if (index >= 0)
                {
                    _warningsDb[index] = entity;
                }
                return Task.FromResult(entity);
            });

        _manager = new DietaryWarningManager(_warningRepository);
    }

    private UserDietaryPreference CreatePreference(
        IEnumerable<string>? terms,
        bool showAlerts = true)
    {
        return new UserDietaryPreference(Guid.NewGuid(), _userId, terms, showAlerts);
    }

    private Product CreateProduct(string? ingredients, string? allergens)
    {
        return new Product(
            Guid.NewGuid(),
            $"779{Random.Shared.Next(10000000, 99999999)}",
            "Producto Prueba",
            ItemCategory.Snacks,
            brand: "Marca Prueba",
            ingredients: ingredients,
            allergens: allergens);
    }

    private PantryItem CreatePantryItem(Guid productId, PantryItemState state = PantryItemState.Active)
    {
        return new PantryItem(Guid.NewGuid(), _userId, productId, state: state);
    }

    [Fact]
    public void Should_Return_NotConfigured_When_Preference_Is_Null()
    {
        var result = _manager.Evaluate("Harina de trigo", "en:gluten", null);

        result.Status.ShouldBe(DietaryVerificationStatus.NotConfigured);
        result.MatchedTerms.ShouldBeEmpty();
        result.WarningMessage.ShouldBeNull();
    }

    [Fact]
    public void Should_Return_NotConfigured_When_Alerts_Are_Disabled()
    {
        var preference = CreatePreference(new[] { "gluten", "mani" }, showAlerts: false);

        var result = _manager.Evaluate("Harina de trigo, maní", "en:gluten", preference);

        result.Status.ShouldBe(DietaryVerificationStatus.NotConfigured);
        result.MatchedTerms.ShouldBeEmpty();
        result.WarningMessage.ShouldBeNull();
    }

    [Fact]
    public void Should_Return_NotConfigured_When_WatchedTerms_Is_Empty()
    {
        var preference = CreatePreference(Array.Empty<string>(), showAlerts: true);

        var result = _manager.Evaluate(null, null, preference);

        result.Status.ShouldBe(DietaryVerificationStatus.NotConfigured);
        result.MatchedTerms.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Return_Warning_When_Term_Matches_Ignoring_Accents_And_Case()
    {
        var preference = CreatePreference(new[] { "Maní", "Azúcar Refinada", "soja" });

        var result = _manager.Evaluate(
            ingredients: "Pasta de MANI tostado, AZÚCAR   REFINADA, aceite vegetal",
            allergens: "en:peanuts",
            preference: preference);

        result.Status.ShouldBe(DietaryVerificationStatus.Warning);
        result.MatchedTerms.ShouldBe(new[] { "mani", "azucar refinada" });
        result.WarningMessage.ShouldNotBeNullOrWhiteSpace();
        result.WarningMessage!.ShouldContain("mani");
        result.WarningMessage!.ShouldContain("azucar refinada");
        result.WarningMessage!.ShouldNotContain("diagnóstico", Case.Insensitive);
        result.WarningMessage!.ShouldNotContain("celíac", Case.Insensitive);
    }

    [Fact]
    public void Should_Return_Warning_When_Term_Matches_Only_In_Allergens()
    {
        var preference = CreatePreference(new[] { "gluten", "leche" });

        var result = _manager.Evaluate(
            ingredients: "Agua, sal marina",
            allergens: "en:gluten, en:milk",
            preference: preference);

        result.Status.ShouldBe(DietaryVerificationStatus.Warning);
        result.MatchedTerms.ShouldBe(new[] { "gluten" });
    }

    [Fact]
    public void Should_Return_Warning_Even_If_Allergens_Is_Missing_When_Ingredients_Matches()
    {
        // BR-002: Coincidencia positiva detectada prevalece aunque el otro campo sea nulo
        var preference = CreatePreference(new[] { "leche" });

        var result = _manager.Evaluate(
            ingredients: "Leche entera pasteurizada",
            allergens: null,
            preference: preference);

        result.Status.ShouldBe(DietaryVerificationStatus.Warning);
        result.MatchedTerms.ShouldBe(new[] { "leche" });
    }

    [Fact]
    public void Should_Return_Warning_Even_If_Ingredients_Is_Missing_When_Allergens_Matches()
    {
        var preference = CreatePreference(new[] { "soybeans" });

        var result = _manager.Evaluate(
            ingredients: null,
            allergens: "en:soybeans",
            preference: preference);

        result.Status.ShouldBe(DietaryVerificationStatus.Warning);
        result.MatchedTerms.ShouldBe(new[] { "soybeans" });
    }

    [Theory]
    [InlineData(null, "en:milk")]
    [InlineData("", "en:milk")]
    [InlineData("   ", "en:milk")]
    [InlineData("Agua, azúcar, jugo de naranja", null)]
    [InlineData("Agua, azúcar, jugo de naranja", "")]
    [InlineData("Agua, azúcar, jugo de naranja", "   ")]
    [InlineData(null, null)]
    public void Should_Return_Unverifiable_When_No_Match_And_Either_Ingredients_Or_Allergens_Is_Missing(
        string? ingredients,
        string? allergens)
    {
        // BR-002: Jamás asumir Compatible cuando falta ingredientes o alérgenos
        var preference = CreatePreference(new[] { "gluten", "mani" });

        var result = _manager.Evaluate(ingredients, allergens, preference);

        result.Status.ShouldBe(DietaryVerificationStatus.Unverifiable);
        result.MatchedTerms.ShouldBeEmpty();
        result.WarningMessage.ShouldBe(DietaryWarningManager.UnverifiableWarningMessage);
    }

    [Fact]
    public void Should_Return_Compatible_Only_When_Both_Fields_Present_And_No_Matches()
    {
        var preference = CreatePreference(new[] { "gluten", "mani", "soja" });

        var result = _manager.Evaluate(
            ingredients: "Agua mineral natural, jugo concentrado de manzana",
            allergens: "none",
            preference: preference);

        result.Status.ShouldBe(DietaryVerificationStatus.Compatible);
        result.MatchedTerms.ShouldBeEmpty();
        result.WarningMessage.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Sync_Item_Dietary_Warning_Idempotently_Across_Lifecycle()
    {
        var preference = CreatePreference(new[] { "gluten" });
        var product = CreateProduct(ingredients: "Harina de trigo", allergens: "en:gluten");
        var item = CreatePantryItem(product.Id);

        // 1. Primera sincronización -> INSERT Warning activo
        var firstEval = await _manager.SyncItemDietaryWarningAsync(item, product, preference);
        firstEval.Status.ShouldBe(DietaryVerificationStatus.Warning);
        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].PantryItemId.ShouldBe(item.Id);
        _warningsDb[0].WarningType.ShouldBe(PreferenceConsts.DietaryPreferenceWarningType);
        _warningsDb[0].IsActive.ShouldBeTrue();
        _warningsDb[0].Message.ShouldContain("gluten");
        var createdWarningId = _warningsDb[0].Id;

        // 2. Segunda sincronización idéntica -> NO duplica ni ejecuta Update innecesario
        await _manager.SyncItemDietaryWarningAsync(item, product, preference);
        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].Id.ShouldBe(createdWarningId);
        await _warningRepository.DidNotReceive().UpdateAsync(Arg.Any<PantryItemWarning>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());

        // 3. Cambio de términos a coincidencia adicional ("trigo") -> UPDATE mensaje manteniendo misma fila
        preference.SetWatchedTerms(new[] { "gluten", "trigo" });
        await _manager.SyncItemDietaryWarningAsync(item, product, preference);
        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].Id.ShouldBe(createdWarningId);
        _warningsDb[0].IsActive.ShouldBeTrue();
        _warningsDb[0].Message.ShouldContain("gluten");
        _warningsDb[0].Message.ShouldContain("trigo");

        // 4. Cambio de preferencia a término sin coincidencia -> Compatible -> DEACTIVATE
        preference.SetWatchedTerms(new[] { "mani" });
        var compatibleEval = await _manager.SyncItemDietaryWarningAsync(item, product, preference);
        compatibleEval.Status.ShouldBe(DietaryVerificationStatus.Compatible);
        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].IsActive.ShouldBeFalse();

        // 5. Reactivación al volver a vigilar "gluten" -> REACTIVATE misma fila
        preference.SetWatchedTerms(new[] { "gluten" });
        await _manager.SyncItemDietaryWarningAsync(item, product, preference);
        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].Id.ShouldBe(createdWarningId);
        _warningsDb[0].IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Create_Unverifiable_Warning_When_Product_Missing_Allergens()
    {
        var preference = CreatePreference(new[] { "lactosa" });
        var product = CreateProduct(ingredients: "Agua, avena", allergens: null);
        var item = CreatePantryItem(product.Id);

        var eval = await _manager.SyncItemDietaryWarningAsync(item, product, preference);

        eval.Status.ShouldBe(DietaryVerificationStatus.Unverifiable);
        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].WarningType.ShouldBe(PreferenceConsts.DietaryPreferenceWarningType);
        _warningsDb[0].IsActive.ShouldBeTrue();
        _warningsDb[0].Message.ShouldBe(DietaryWarningManager.UnverifiableWarningMessage);
    }

    [Fact]
    public async Task Should_Deactivate_Dietary_Warning_When_Item_Is_Consumed_Or_Discarded()
    {
        var preference = CreatePreference(new[] { "gluten" });
        var product = CreateProduct(ingredients: "Harina de trigo", allergens: "en:gluten");
        var item = CreatePantryItem(product.Id);

        await _manager.SyncItemDietaryWarningAsync(item, product, preference);
        _warningsDb[0].IsActive.ShouldBeTrue();

        item.Consume();
        await _manager.SyncItemDietaryWarningAsync(item, product, preference);
        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].IsActive.ShouldBeFalse();

        item.Discard();
        await _manager.SyncItemDietaryWarningAsync(item, product, preference);
        _warningsDb[0].IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Sync_Batch_Items_And_Return_Active_Warning_Count()
    {
        var preference = CreatePreference(new[] { "gluten" });

        var pWarning = CreateProduct(ingredients: "Trigo", allergens: "en:gluten");
        var pUnverifiable = CreateProduct(ingredients: "Agua", allergens: null);
        var pCompatible = CreateProduct(ingredients: "Agua, sal", allergens: "none");

        var item1 = CreatePantryItem(pWarning.Id, PantryItemState.Active);
        var item2 = CreatePantryItem(pUnverifiable.Id, PantryItemState.Active);
        var item3 = CreatePantryItem(pCompatible.Id, PantryItemState.Active);
        var item4Consumed = CreatePantryItem(pWarning.Id, PantryItemState.Consumed);

        var items = new[] { item1, item2, item3, item4Consumed };
        var productsById = new Dictionary<Guid, Product>
        {
            [pWarning.Id] = pWarning,
            [pUnverifiable.Id] = pUnverifiable,
            [pCompatible.Id] = pCompatible
        };

        var activeCount = await _manager.SyncItemsDietaryWarningsAsync(items, productsById, preference);

        activeCount.ShouldBe(2); // item1 (Warning) + item2 (Unverifiable)
        _warningsDb.Count.ShouldBe(2);
        _warningsDb.ShouldContain(w => w.PantryItemId == item1.Id && w.IsActive && w.Message.Contains("gluten"));
        _warningsDb.ShouldContain(w => w.PantryItemId == item2.Id && w.IsActive && w.Message == DietaryWarningManager.UnverifiableWarningMessage);

        // Desactivar alertas en pantalla -> todas pasan a inactivas (count == 0) sin borrar filas
        preference.SetShowAlertsOnScreen(false);
        var afterDisableCount = await _manager.SyncItemsDietaryWarningsAsync(items, productsById, preference);

        afterDisableCount.ShouldBe(0);
        _warningsDb.Count.ShouldBe(2);
        _warningsDb.ShouldAllBe(w => !w.IsActive);
    }

    [Fact]
    public void Should_Normalize_Comparison_Strings_Consistently()
    {
        DietaryWarningManager.NormalizeForComparison(null).ShouldBe(string.Empty);
        DietaryWarningManager.NormalizeForComparison("   ").ShouldBe(string.Empty);
        DietaryWarningManager.NormalizeForComparison("  LÉCHE   Descremada\t(Maní) ").ShouldBe("leche descremada (mani)");
    }
}
