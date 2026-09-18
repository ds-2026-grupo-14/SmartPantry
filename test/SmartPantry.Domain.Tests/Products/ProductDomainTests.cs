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

       
        var product = new Product(
            id,
            rawBarcode,
            rawName,
            ItemCategory.Dairy,
            rawBrand,
            rawQuantity
        );

        
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

    [Fact]
    public void Should_Update_Valid_Product_And_Preserve_Normalizations()
    {
        // Arrange
        var product = new Product(
            Guid.NewGuid(),
            "7791234567890",
            "Leche Entera",
            ItemCategory.Dairy,
            "La Serenísima",
            "1L",
            "Leche entera pasteurizada",
            "Lactosa"
        );

        //  Modificación válida con espacios en blanco para probar normalización (Trim)
        product.Update(
            name: "  Leche Descremada  ",
            category: ItemCategory.Beverages,
            brand: "  La Serenísima Clásica  ",
            quantity: "  750ml  ",
            ingredients: "  Leche descremada, calcio  ",
            allergens: "  Lactosa  "
        );

        //  Se deben conservar las normalizaciones y la categoría actualizada
        product.Name.ShouldBe("Leche Descremada");
        product.Category.ShouldBe(ItemCategory.Beverages);
        product.Brand.ShouldBe("La Serenísima Clásica");
        product.Quantity.ShouldBe("750ml");
        product.Ingredients.ShouldBe("Leche descremada, calcio");
        product.Allergens.ShouldBe("Lactosa");
        product.Barcode.ShouldBe("7791234567890"); // El Barcode se mantiene inalterado
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Reject_Invalid_Name_On_Update_And_Maintain_Previous_State(string? invalidName)
    {
        
        var product = new Product(
            Guid.NewGuid(),
            "7791234567890",
            "Leche Entera",
            ItemCategory.Dairy,
            "La Serenísima",
            "1L",
            "Leche entera pasteurizada",
            "Lactosa"
        );

        // Debe lanzar ArgumentException
        Should.Throw<ArgumentException>(() =>
        {
            product.Update(
                invalidName!,
                ItemCategory.Beverages,
                "Marca Modificada",
                "500ml"
            );
        });

        //El estado previo debe mantenerse intacto (no mutación parcial)
        product.Name.ShouldBe("Leche Entera");
        product.Category.ShouldBe(ItemCategory.Dairy);
        product.Brand.ShouldBe("La Serenísima");
        product.Quantity.ShouldBe("1L");
        product.Ingredients.ShouldBe("Leche entera pasteurizada");
        product.Allergens.ShouldBe("Lactosa");
        product.Barcode.ShouldBe("7791234567890");
    }

    [Fact]
    public void Should_Reject_Invalid_Category_On_Update_And_Maintain_Previous_State()
    {
        // Arrange
        var product = new Product(
            Guid.NewGuid(),
            "7791234567890",
            "Leche Entera",
            ItemCategory.Dairy,
            "La Serenísima",
            "1L"
        );

        
        Should.Throw<ArgumentException>(() =>
        {
            product.Update(
                "Leche Nueva",
                ItemCategory.Undefined,
                "Nueva Marca"
            );
        });

        // El estado previo debe mantenerse intacto
        product.Name.ShouldBe("Leche Entera");
        product.Category.ShouldBe(ItemCategory.Dairy);
        product.Brand.ShouldBe("La Serenísima");
        product.Quantity.ShouldBe("1L");
    }

    [Fact]
    public void Should_Reject_Length_Exceeded_On_Update_And_Maintain_Previous_State()
    {
        // Arrange
        var product = new Product(
            Guid.NewGuid(),
            "7791234567890",
            "Leche Entera",
            ItemCategory.Dairy,
            "La Serenísima",
            "1L"
        );

        var tooLongName = new string('A', ProductConsts.MaxNameLength + 1);

        
        Should.Throw<ArgumentException>(() =>
        {
            product.Update(tooLongName, ItemCategory.Dairy);
        });

        // Mantiene el estado previo
        product.Name.ShouldBe("Leche Entera");
        product.Category.ShouldBe(ItemCategory.Dairy);
    }
}

