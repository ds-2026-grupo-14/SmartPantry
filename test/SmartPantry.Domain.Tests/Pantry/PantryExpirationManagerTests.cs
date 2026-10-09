using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using SmartPantry.Products;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace SmartPantry.Pantry;

public class PantryExpirationManagerTests
{
    private readonly IRepository<PantryItem, Guid> _pantryItemRepository;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<PantryItemWarning, Guid> _warningRepository;
    private readonly PantryExpirationManager _manager;

    private readonly List<PantryItem> _pantryItemsDb = new();
    private readonly List<Product> _productsDb = new();
    private readonly List<PantryItemWarning> _warningsDb = new();

    private readonly DateTime _referenceDate = new(2026, 10, 15); // Fecha local controlada
    private readonly Guid _userId = Guid.NewGuid();

    public PantryExpirationManagerTests()
    {
        _pantryItemRepository = Substitute.For<IRepository<PantryItem, Guid>>();
        _productRepository = Substitute.For<IRepository<Product, Guid>>();
        _warningRepository = Substitute.For<IRepository<PantryItemWarning, Guid>>();

        // Configuración de lecturas sobre listas en memoria
        _pantryItemRepository.GetListAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(_pantryItemsDb.ToList()));

        _productRepository.GetListAsync(Arg.Any<Expression<Func<Product, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var expr = callInfo.Arg<Expression<Func<Product, bool>>>().Compile();
                return Task.FromResult(_productsDb.Where(expr).ToList());
            });

        _warningRepository.GetListAsync(Arg.Any<Expression<Func<PantryItemWarning, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var expr = callInfo.Arg<Expression<Func<PantryItemWarning, bool>>>().Compile();
                return Task.FromResult(_warningsDb.Where(expr).ToList());
            });

        // Configuración de inserciones y actualizaciones en memoria
        _warningRepository.InsertAsync(Arg.Any<PantryItemWarning>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var entity = callInfo.Arg<PantryItemWarning>();
                _warningsDb.Add(entity);
                return Task.FromResult(entity);
            });

        _warningRepository.UpdateAsync(Arg.Any<PantryItemWarning>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
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

        _manager = new PantryExpirationManager(
            _pantryItemRepository,
            _productRepository,
            _warningRepository);
    }

    private Product CreateProduct(ItemCategory category = ItemCategory.Dairy)
    {
        var product = new Product(
            Guid.NewGuid(),
            $"779{Random.Shared.Next(10000000, 99999999)}",
            "Producto de Prueba",
            category);
        _productsDb.Add(product);
        return product;
    }

    private PantryItem CreatePantryItem(
        Guid productId,
        DateTime? expirationDate,
        PantryItemState state = PantryItemState.Active)
    {
        var item = new PantryItem(
            Guid.NewGuid(),
            _userId,
            productId,
            expirationDate,
            state);
        _pantryItemsDb.Add(item);
        return item;
    }

    [Fact]
    public async Task Should_Create_Warning_When_Within_Threshold()
    {
        // Lácteo tiene umbral de 2 días. Vence en 1 día -> debe alertar.
        var product = CreateProduct(ItemCategory.Dairy);
        var item = CreatePantryItem(product.Id, _referenceDate.AddDays(1));

        var count = await _manager.ProcessExpirationsAsync(_referenceDate);

        count.ShouldBe(1);
        _warningsDb.Count.ShouldBe(1);
        var warning = _warningsDb.First();
        warning.PantryItemId.ShouldBe(item.Id);
        warning.WarningType.ShouldBe(PantryWarningConsts.ExpirationWarningType);
        warning.IsActive.ShouldBeTrue();
        warning.Message.ShouldContain("Próximo a vencer el");
    }

    [Fact]
    public async Task Should_Create_Warning_When_Expired()
    {
        // Venció hace 1 día
        var product = CreateProduct(ItemCategory.Bakery);
        var item = CreatePantryItem(product.Id, _referenceDate.AddDays(-1));

        var count = await _manager.ProcessExpirationsAsync(_referenceDate);

        count.ShouldBe(1);
        _warningsDb.Count.ShouldBe(1);
        var warning = _warningsDb.First();
        warning.PantryItemId.ShouldBe(item.Id);
        warning.IsActive.ShouldBeTrue();
        warning.Message.ShouldContain("Vencido el");
    }

    [Fact]
    public async Task Should_Be_Idempotent_When_Executed_Multiple_Times()
    {
        var product = CreateProduct(ItemCategory.Dairy);
        CreatePantryItem(product.Id, _referenceDate.AddDays(1));

        // Primera corrida
        var firstRun = await _manager.ProcessExpirationsAsync(_referenceDate);
        firstRun.ShouldBe(1);
        _warningsDb.Count.ShouldBe(1);
        var firstWarningId = _warningsDb[0].Id;

        // Segunda corrida inmediata (mismos datos)
        var secondRun = await _manager.ProcessExpirationsAsync(_referenceDate);
        secondRun.ShouldBe(1);
        _warningsDb.Count.ShouldBe(1); // No debe duplicar registros
        _warningsDb[0].Id.ShouldBe(firstWarningId);
    }

    [Fact]
    public async Task Should_Update_Warning_When_Expiration_Date_Changes()
    {
        var product = CreateProduct(ItemCategory.Dairy);
        var item = CreatePantryItem(product.Id, _referenceDate.AddDays(1));

        await _manager.ProcessExpirationsAsync(_referenceDate);
        _warningsDb[0].Message.ShouldContain(_referenceDate.AddDays(1).ToString("dd/MM/yyyy"));

        // Se cambia la fecha de vencimiento a 2 días
        item.SetExpirationDate(_referenceDate.AddDays(2));

        await _manager.ProcessExpirationsAsync(_referenceDate);

        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].Message.ShouldContain(_referenceDate.AddDays(2).ToString("dd/MM/yyyy"));
    }

    [Fact]
    public async Task Should_Deactivate_Warning_When_Consumed_Or_Discarded()
    {
        var product = CreateProduct(ItemCategory.Dairy);
        var item = CreatePantryItem(product.Id, _referenceDate.AddDays(1));

        await _manager.ProcessExpirationsAsync(_referenceDate);
        _warningsDb[0].IsActive.ShouldBeTrue();

        // Se marca como Consumed (RN-6: se conserva el registro pero desactiva la advertencia)
        item.Consume();
        await _manager.ProcessExpirationsAsync(_referenceDate);

        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].IsActive.ShouldBeFalse();

        // Si pasa a Discarded, continúa inactiva
        item.Discard();
        await _manager.ProcessExpirationsAsync(_referenceDate);
        _warningsDb[0].IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Deactivate_Warning_When_Postponed_Beyond_Threshold()
    {
        var product = CreateProduct(ItemCategory.Dairy);
        var item = CreatePantryItem(product.Id, _referenceDate.AddDays(1));

        await _manager.ProcessExpirationsAsync(_referenceDate);
        _warningsDb[0].IsActive.ShouldBeTrue();

        // Se posterga la fecha a 6 meses después (fuera del umbral)
        item.SetExpirationDate(_referenceDate.AddMonths(6));
        await _manager.ProcessExpirationsAsync(_referenceDate);

        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Reactivate_Warning_When_Date_Falls_Back_In_Threshold()
    {
        var product = CreateProduct(ItemCategory.Dairy);
        var item = CreatePantryItem(product.Id, _referenceDate.AddMonths(6)); // Lejos
        var warning = new PantryItemWarning(
            Guid.NewGuid(),
            item.Id,
            PantryWarningConsts.ExpirationWarningType,
            "Alerta vieja",
            isActive: false);
        _warningsDb.Add(warning);

        // La fecha vuelve a entrar en umbral
        item.SetExpirationDate(_referenceDate.AddDays(1));
        await _manager.ProcessExpirationsAsync(_referenceDate);

        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].IsActive.ShouldBeTrue();
        _warningsDb[0].Message.ShouldContain("Próximo a vencer el");
    }

    [Fact]
    public async Task Should_Not_Create_Warning_For_Null_ExpirationDate()
    {
        var product = CreateProduct(ItemCategory.DryGoods);
        CreatePantryItem(product.Id, expirationDate: null);

        var count = await _manager.ProcessExpirationsAsync(_referenceDate);

        count.ShouldBe(0);
        _warningsDb.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Apply_Category_Specific_Thresholds()
    {
        // DryGoods tiene umbral de 7 días. Con 5 días debe alertar.
        var dryProduct = CreateProduct(ItemCategory.DryGoods);
        var dryItem = CreatePantryItem(dryProduct.Id, _referenceDate.AddDays(5));

        // Dairy tiene umbral de 2 días. Con 5 días NO debe alertar.
        var dairyProduct = CreateProduct(ItemCategory.Dairy);
        var dairyItem = CreatePantryItem(dairyProduct.Id, _referenceDate.AddDays(5));

        var count = await _manager.ProcessExpirationsAsync(_referenceDate);

        count.ShouldBe(1);
        _warningsDb.Count.ShouldBe(1);
        _warningsDb[0].PantryItemId.ShouldBe(dryItem.Id);
    }
}
