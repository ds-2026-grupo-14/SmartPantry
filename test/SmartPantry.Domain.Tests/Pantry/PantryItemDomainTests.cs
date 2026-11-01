using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace SmartPantry.Pantry;

public class PantryItemDomainTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    [Fact]
    public void Should_Create_PantryItem_With_Quantity_And_Unit()
    {
        var item = new PantryItem(
            Guid.NewGuid(),
            _userId,
            _productId,
            DateTime.Today.AddDays(10),
            PantryItemState.Active,
            quantity: 2.5m,
            unit: " kg "
        );

        item.Quantity.ShouldBe(2.5m);
        item.Unit.ShouldBe("kg");
        item.State.ShouldBe(PantryItemState.Active);
    }

    [Fact]
    public void Should_Allow_Null_Quantity_And_Unit()
    {
        var item = new PantryItem(
            Guid.NewGuid(),
            _userId,
            _productId,
            null,
            PantryItemState.Active,
            quantity: null,
            unit: null
        );

        item.Quantity.ShouldBeNull();
        item.Unit.ShouldBeNull();
        item.ExpirationDate.ShouldBeNull();
    }

    [Fact]
    public void Should_Reject_Negative_Quantity()
    {
        var ex = Should.Throw<BusinessException>(() =>
        {
            new PantryItem(
                Guid.NewGuid(),
                _userId,
                _productId,
                quantity: -1m
            );
        });

        ex.Code.ShouldBe("SmartPantry:PantryItem:NegativeQuantity");
    }

    [Fact]
    public void Should_Reject_Unit_Exceeding_Max_Length()
    {
        var tooLongUnit = new string('a', PantryConsts.MaxUnitLength + 1);

        var ex = Should.Throw<BusinessException>(() =>
        {
            new PantryItem(
                Guid.NewGuid(),
                _userId,
                _productId,
                unit: tooLongUnit
            );
        });

        ex.Code.ShouldBe("SmartPantry:PantryItem:UnitTooLong");
    }

    [Fact]
    public void Should_Update_Quantity_And_Unit_Validly()
    {
        var item = new PantryItem(Guid.NewGuid(), _userId, _productId);

        item.SetQuantity(10m);
        item.SetUnit(" paquetes ");

        item.Quantity.ShouldBe(10m);
        item.Unit.ShouldBe("paquetes");

        item.SetQuantity(null);
        item.SetUnit(null);

        item.Quantity.ShouldBeNull();
        item.Unit.ShouldBeNull();
    }

    [Fact]
    public void Should_Transition_States_Correctly()
    {
        var item = new PantryItem(Guid.NewGuid(), _userId, _productId);
        item.State.ShouldBe(PantryItemState.Active);

        item.Consume();
        item.State.ShouldBe(PantryItemState.Consumed);

        item.Discard();
        item.State.ShouldBe(PantryItemState.Discarded);

        item.SetActive();
        item.State.ShouldBe(PantryItemState.Active);
    }
}
