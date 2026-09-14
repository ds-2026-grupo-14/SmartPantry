using System;
using Shouldly;
using SmartPantry.Products;
using Volo.Abp.Modularity;
using Xunit;

namespace SmartPantry.Products;

public abstract class ProductDomainTests<TStartupModule> : SmartPantryDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public void Should_Create_Valid_Product_And_Normalize_Text_Data()
    {
        // Arrange
        var id = Guid.NewGuid();
        var rawBarcode = " 7791234567890 ";
        var rawName = " Leche Entera ";
        var rawBrand = " La Serenísima ";
        var rawQuantity = " 1L ";

        // Act
        var product = new Product(
            id,
            rawBarcode,
            rawName,
            ItemCategory.Dairy,
            rawBrand,
            rawQuantity
        );

        // Assert
        product.Id.ShouldBe(id);
        product.Barcode.ShouldBe("7791234567890");
        product.Name.ShouldBe("Leche Entera");
        product.Brand.ShouldBe("La Serenísima");
        product.Quantity.ShouldBe("1L");
        product.Category.ShouldBe(ItemCategory.Dairy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Reject_Empty_Or_Whitespace_Barcode(string? invalidBarcode)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
        {
            new Product(
                Guid.NewGuid(),
                invalidBarcode!,
                "Producto Válido",
                ItemCategory.Dairy
            );
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Reject_Empty_Or_Whitespace_Name(string? invalidName)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
        {
            new Product(
                Guid.NewGuid(),
                "7791234567890",
                invalidName!,
                ItemCategory.Dairy
            );
        });
    }
}

