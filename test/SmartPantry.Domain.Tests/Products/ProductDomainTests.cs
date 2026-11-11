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

    [Fact]
    public void Should_Create_And_Update_Product_With_Nutritional_Metadata_And_Normalize_Values()
    {
        // Arrange
        var id = Guid.NewGuid();
        var product = new Product(
            id,
            "7791234567890",
            "Yogur Firme",
            ItemCategory.Dairy,
            "La Serenísima",
            "190g",
            "Leche, fermentos",
            "Lactosa",
            externalCategories: "  Lácteos, Yogures  ",
            imageUrl: "  https://images.openfoodfacts.org/front.jpg  ",
            nutriScore: "  b  ",
            novaGroup: 2
        );

        // Assert creation
        product.ExternalCategories.ShouldBe("Lácteos, Yogures");
        product.ImageUrl.ShouldBe("https://images.openfoodfacts.org/front.jpg");
        product.NutriScore.ShouldBe("B");
        product.NovaGroup.ShouldBe(2);

        // Act update
        product.Update(
            name: "Yogur Firme Descremado",
            category: ItemCategory.Dairy,
            brand: "La Serenísima",
            quantity: "190g",
            ingredients: "Leche descremada, fermentos",
            allergens: "Lactosa",
            externalCategories: "  Postres, Yogures  ",
            imageUrl: "  http://images.openfoodfacts.org/front2.jpg  ",
            nutriScore: "  a  ",
            novaGroup: 1
        );

        // Assert update
        product.Name.ShouldBe("Yogur Firme Descremado");
        product.ExternalCategories.ShouldBe("Postres, Yogures");
        product.ImageUrl.ShouldBe("http://images.openfoodfacts.org/front2.jpg");
        product.NutriScore.ShouldBe("A");
        product.NovaGroup.ShouldBe(1);
    }

    [Fact]
    public void Should_Normalize_Empty_Or_Whitespace_Nutritional_Metadata_To_Null()
    {
        // Arrange
        var product = new Product(
            Guid.NewGuid(),
            "7791234567890",
            "Producto Test",
            ItemCategory.Dairy,
            externalCategories: "   ",
            imageUrl: "   ",
            nutriScore: "   ",
            novaGroup: null
        );

        product.ExternalCategories.ShouldBeNull();
        product.ImageUrl.ShouldBeNull();
        product.NutriScore.ShouldBeNull();
        product.NovaGroup.ShouldBeNull();

        product.SetNutritionalMetadata(
            imageUrl: "  https://example.com/img.jpg  ",
            nutriScore: "  c  ",
            novaGroup: 3,
            externalCategories: "  Snacks  "
        );

        product.ImageUrl.ShouldBe("https://example.com/img.jpg");
        product.NutriScore.ShouldBe("C");
        product.NovaGroup.ShouldBe(3);
        product.ExternalCategories.ShouldBe("Snacks");

        product.SetExternalCategories("   ");
        product.SetImageUrl(string.Empty);
        product.SetNutriScore(null);
        product.SetNovaGroup(null);

        product.ExternalCategories.ShouldBeNull();
        product.ImageUrl.ShouldBeNull();
        product.NutriScore.ShouldBeNull();
        product.NovaGroup.ShouldBeNull();
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("F")]
    [InlineData("unknown")]
    [InlineData("AB")]
    [InlineData("1")]
    public void Should_Reject_Invalid_NutriScore_And_Maintain_Previous_State(string invalidNutriScore)
    {
        // Arrange
        var product = new Product(
            Guid.NewGuid(),
            "7791234567890",
            "Leche Entera",
            ItemCategory.Dairy,
            nutriScore: "A",
            novaGroup: 1
        );

        // Act & Assert SetNutriScore
        Should.Throw<ArgumentException>(() =>
        {
            product.SetNutriScore(invalidNutriScore);
        });
        product.NutriScore.ShouldBe("A");

        // Act & Assert Update
        Should.Throw<ArgumentException>(() =>
        {
            product.Update(
                "Leche Modificada",
                ItemCategory.Beverages,
                nutriScore: invalidNutriScore
            );
        });

        product.Name.ShouldBe("Leche Entera");
        product.Category.ShouldBe(ItemCategory.Dairy);
        product.NutriScore.ShouldBe("A");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Should_Reject_Invalid_NovaGroup_And_Maintain_Previous_State(int invalidNovaGroup)
    {
        // Arrange
        var product = new Product(
            Guid.NewGuid(),
            "7791234567890",
            "Leche Entera",
            ItemCategory.Dairy,
            novaGroup: 2
        );

        // Act & Assert SetNovaGroup
        Should.Throw<ArgumentException>(() =>
        {
            product.SetNovaGroup(invalidNovaGroup);
        });
        product.NovaGroup.ShouldBe(2);

        // Act & Assert Update
        Should.Throw<ArgumentException>(() =>
        {
            product.Update(
                "Leche Modificada",
                ItemCategory.Beverages,
                novaGroup: invalidNovaGroup
            );
        });

        product.Name.ShouldBe("Leche Entera");
        product.Category.ShouldBe(ItemCategory.Dairy);
        product.NovaGroup.ShouldBe(2);
    }

    [Theory]
    [InlineData("ftp://example.com/image.jpg")]
    [InlineData("not-a-valid-uri")]
    [InlineData("file:///C:/local/image.jpg")]
    public void Should_Reject_Invalid_ImageUrl_Scheme_And_Maintain_Previous_State(string invalidImageUrl)
    {
        // Arrange
        var product = new Product(
            Guid.NewGuid(),
            "7791234567890",
            "Leche Entera",
            ItemCategory.Dairy,
            imageUrl: "https://example.com/initial.jpg"
        );

        Should.Throw<ArgumentException>(() =>
        {
            product.SetImageUrl(invalidImageUrl);
        });
        product.ImageUrl.ShouldBe("https://example.com/initial.jpg");

        Should.Throw<ArgumentException>(() =>
        {
            product.Update(
                "Leche Modificada",
                ItemCategory.Beverages,
                imageUrl: invalidImageUrl
            );
        });
        product.Name.ShouldBe("Leche Entera");
        product.ImageUrl.ShouldBe("https://example.com/initial.jpg");
    }

    [Fact]
    public void Should_Reject_Too_Long_Nutritional_Fields_And_Maintain_Previous_State()
    {
        // Arrange
        var product = new Product(
            Guid.NewGuid(),
            "7791234567890",
            "Leche Entera",
            ItemCategory.Dairy,
            externalCategories: "Lácteos",
            imageUrl: "https://example.com/initial.jpg"
        );

        var tooLongCategories = new string('C', ProductConsts.MaxExternalCategoriesLength + 1);
        var tooLongImageUrl = "https://example.com/" + new string('x', ProductConsts.MaxImageUrlLength);

        // ExternalCategories
        Should.Throw<ArgumentException>(() =>
        {
            product.SetExternalCategories(tooLongCategories);
        });
        product.ExternalCategories.ShouldBe("Lácteos");

        // ImageUrl
        Should.Throw<ArgumentException>(() =>
        {
            product.SetImageUrl(tooLongImageUrl);
        });
        product.ImageUrl.ShouldBe("https://example.com/initial.jpg");

        // Update with too long categories
        Should.Throw<ArgumentException>(() =>
        {
            product.Update(
                "Nuevo Nombre",
                ItemCategory.Beverages,
                externalCategories: tooLongCategories
            );
        });
        product.Name.ShouldBe("Leche Entera");
        product.ExternalCategories.ShouldBe("Lácteos");
    }

    [Fact]
    public void Should_Maintain_Atomic_State_When_SetNutritionalMetadata_Fails()
    {
        // Arrange
        var product = new Product(
            Guid.NewGuid(),
            "7791234567890",
            "Leche Entera",
            ItemCategory.Dairy,
            externalCategories: "Original Cat",
            imageUrl: "https://example.com/orig.jpg",
            nutriScore: "A",
            novaGroup: 1
        );

        // Act & Assert: first argument is valid, but second argument (nutriScore) is invalid
        Should.Throw<ArgumentException>(() =>
        {
            product.SetNutritionalMetadata(
                imageUrl: "https://example.com/new.jpg",
                nutriScore: "INVALID",
                novaGroup: 3,
                externalCategories: "New Cat"
            );
        });

        // None of the properties should have mutated
        product.ImageUrl.ShouldBe("https://example.com/orig.jpg");
        product.NutriScore.ShouldBe("A");
        product.NovaGroup.ShouldBe(1);
        product.ExternalCategories.ShouldBe("Original Cat");
    }
}

