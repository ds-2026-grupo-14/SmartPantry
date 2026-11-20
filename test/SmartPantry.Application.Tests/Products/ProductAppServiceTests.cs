using System;
using System.Threading.Tasks;
using Shouldly;
using SmartPantry.Products;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.Validation;
using Xunit;

namespace SmartPantry.Products;

public abstract class ProductAppServiceTests<TStartupModule> : SmartPantryApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IProductAppService _productAppService;
    private readonly IRepository<Product, Guid> _productRepository;

    protected ProductAppServiceTests()
    {
        _productAppService = GetRequiredService<IProductAppService>();
        _productRepository = GetRequiredService<IRepository<Product, Guid>>();
    }

    [Fact]
    public async Task Should_Create_Product_And_Get_It_By_Id()
    {
        // Arrange
        var input = new CreateProductDto
        {
            Barcode = "7798765432109",
            Name = "Yogur Frutilla",
            Category = ItemCategory.Dairy,
            Brand = "Yogs",
            Quantity = "200g"
        };

        var createdDto = await _productAppService.CreateAsync(input);

        //Comprobar que fue creado
        createdDto.ShouldNotBeNull();
        createdDto.Id.ShouldNotBe(Guid.Empty);
        createdDto.Barcode.ShouldBe("7798765432109");
        createdDto.Name.ShouldBe("Yogur Frutilla");

        // Recuperar por Id desde el AppService
        var retrievedDto = await _productAppService.GetAsync(createdDto.Id);
        retrievedDto.ShouldNotBeNull();
        retrievedDto.Id.ShouldBe(createdDto.Id);
        retrievedDto.Barcode.ShouldBe("7798765432109");

        // Recuperar por Id desde el Repositorio configurado por ABP
        var entityInDb = await _productRepository.FindAsync(createdDto.Id);
        entityInDb.ShouldNotBeNull();
        entityInDb.Name.ShouldBe("Yogur Frutilla");
    }

    [Fact]
    public async Task Should_Not_Allow_Creating_Product_When_Required_Dto_Field_Is_Missing()
    {
        // Arrange - Barcode es string.Empty (o null), lo cual invalida [Required] en el DTO
        var invalidInput = new CreateProductDto
        {
            Barcode = string.Empty,
            Name = "Producto Inválido",
            Category = ItemCategory.Dairy
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _productAppService.CreateAsync(invalidInput);
        });
    }

    [Fact]
    public async Task Should_Execute_Full_Product_Lifecycle_Crud_Flow()
    {
        
        // 1. Crear (CreateAsync)
       
        var createInput = new CreateProductDto
        {
            Barcode = "7791234567899",
            Name = "Arroz Integral",
            Category = ItemCategory.DryGoods,
            Brand = "Gallo",
            Quantity = "1kg",
            Ingredients = "Arroz integral",
            Allergens = "Ninguno",
            ExternalCategories = "Cereales, Arroces",
            ImageUrl = "https://images.openfoodfacts.org/arroz.jpg",
            NutriScore = "a",
            NovaGroup = 1
        };

        var createdProduct = await _productAppService.CreateAsync(createInput);

        createdProduct.ShouldNotBeNull();
        createdProduct.Id.ShouldNotBe(Guid.Empty);
        createdProduct.Barcode.ShouldBe("7791234567899");
        createdProduct.Name.ShouldBe("Arroz Integral");
        createdProduct.Category.ShouldBe(ItemCategory.DryGoods);
        createdProduct.Brand.ShouldBe("Gallo");
        createdProduct.Quantity.ShouldBe("1kg");
        createdProduct.Ingredients.ShouldBe("Arroz integral");
        createdProduct.Allergens.ShouldBe("Ninguno");
        createdProduct.ExternalCategories.ShouldBe("Cereales, Arroces");
        createdProduct.ImageUrl.ShouldBe("https://images.openfoodfacts.org/arroz.jpg");
        createdProduct.NutriScore.ShouldBe("A");
        createdProduct.NovaGroup.ShouldBe(1);

        var productId = createdProduct.Id;

        
        // 2. Listar paginado (GetListAsync)
        
        var listResult = await _productAppService.GetListAsync(new PagedAndSortedResultRequestDto
        {
            MaxResultCount = 10,
            SkipCount = 0
        });

        listResult.ShouldNotBeNull();
        listResult.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
        listResult.Items.ShouldContain(p => p.Id == productId);

        
        // 3. Modificar (UpdateAsync)
        
        var updateInput = new UpdateProductDto
        {
            Name = "Arroz Integral Doble Carolina",
            Category = ItemCategory.DryGoods,
            Brand = "Gallo Oro",
            Quantity = "500g",
            Ingredients = "Arroz parboil seleccionado",
            Allergens = "Puede contener trazas de soja",
            ExternalCategories = "Arroces seleccionados",
            ImageUrl = "https://images.openfoodfacts.org/arroz-v2.jpg",
            NutriScore = "b",
            NovaGroup = 2
        };

        var updatedProduct = await _productAppService.UpdateAsync(productId, updateInput);

        updatedProduct.ShouldNotBeNull();
        updatedProduct.Id.ShouldBe(productId);
        updatedProduct.Name.ShouldBe("Arroz Integral Doble Carolina");
        updatedProduct.Category.ShouldBe(ItemCategory.DryGoods);
        updatedProduct.Brand.ShouldBe("Gallo Oro");
        updatedProduct.Quantity.ShouldBe("500g");
        updatedProduct.Ingredients.ShouldBe("Arroz parboil seleccionado");
        updatedProduct.Allergens.ShouldBe("Puede contener trazas de soja");
        updatedProduct.ExternalCategories.ShouldBe("Arroces seleccionados");
        updatedProduct.ImageUrl.ShouldBe("https://images.openfoodfacts.org/arroz-v2.jpg");
        updatedProduct.NutriScore.ShouldBe("B");
        updatedProduct.NovaGroup.ShouldBe(2);

        
        // 4. Consultar (GetAsync)
        
        var retrievedProduct = await _productAppService.GetAsync(productId);

        retrievedProduct.ShouldNotBeNull();
        retrievedProduct.Id.ShouldBe(productId);
        retrievedProduct.Barcode.ShouldBe("7791234567899"); // El Barcode se mantiene inmutable
        retrievedProduct.Name.ShouldBe("Arroz Integral Doble Carolina");
        retrievedProduct.Category.ShouldBe(ItemCategory.DryGoods);
        retrievedProduct.Brand.ShouldBe("Gallo Oro");
        retrievedProduct.Quantity.ShouldBe("500g");
        retrievedProduct.Ingredients.ShouldBe("Arroz parboil seleccionado");
        retrievedProduct.Allergens.ShouldBe("Puede contener trazas de soja");
        retrievedProduct.ExternalCategories.ShouldBe("Arroces seleccionados");
        retrievedProduct.ImageUrl.ShouldBe("https://images.openfoodfacts.org/arroz-v2.jpg");
        retrievedProduct.NutriScore.ShouldBe("B");
        retrievedProduct.NovaGroup.ShouldBe(2);

        
        // 5. Eliminar (DeleteAsync)
        
        await _productAppService.DeleteAsync(productId);

        
        // 6. Verificar que GetAsync sobre el eliminado falle lanzando EntityNotFoundException
        
        await Should.ThrowAsync<EntityNotFoundException>(async () =>
        {
            await _productAppService.GetAsync(productId);
        });

        // Verificación adicional a nivel de persistencia en el repositorio
        var entityInDb = await _productRepository.FindAsync(productId);
        entityInDb.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Reject_Duplicate_Barcode_On_CreateAsync()
    {
        // Arrange
        await _productAppService.CreateAsync(new CreateProductDto
        {
            Barcode = "7795555111222",
            Name = "Producto Inicial",
            Category = ItemCategory.Beverages
        });

        // Act & Assert
        var exception = await Should.ThrowAsync<BusinessException>(async () =>
        {
            await _productAppService.CreateAsync(new CreateProductDto
            {
                Barcode = "  7795555111222  ",
                Name = "Producto Duplicado",
                Category = ItemCategory.Beverages
            });
        });

        exception.Code.ShouldBe(SmartPantryDomainErrorCodes.DuplicateProductBarcode);
    }

    [Fact]
    public async Task Should_Save_From_External_Idempotently_And_Enrich_Missing_Metadata()
    {
        // Arrange & Act 1: First external save with partial metadata
        var firstInput = new SaveExternalProductDto
        {
            Barcode = "  7790895000997  ",
            Name = "Galletitas Dulces Rellenas Sabor Chocolate",
            Category = ItemCategory.Snacks,
            Brand = "Bagley",
            Quantity = null,
            Ingredients = null,
            Allergens = null,
            ExternalCategories = null,
            ImageUrl = null,
            NutriScore = null,
            NovaGroup = null
        };

        var firstResult = await _productAppService.SaveFromExternalAsync(firstInput);

        firstResult.ShouldNotBeNull();
        firstResult.Id.ShouldNotBe(Guid.Empty);
        firstResult.Barcode.ShouldBe("7790895000997");
        firstResult.Brand.ShouldBe("Bagley");
        firstResult.NutriScore.ShouldBeNull();
        firstResult.NovaGroup.ShouldBeNull();

        // Act 2: Second external save with same barcode, different Name/Brand, and missing metadata filled
        var secondInput = new SaveExternalProductDto
        {
            Barcode = "7790895000997",
            Name = "Nombre Alternativo No Debe Sobrescribir",
            Category = ItemCategory.Other,
            Brand = "Otra Marca No Debe Sobrescribir",
            Quantity = "118 g",
            Ingredients = "Harina de trigo enriquecida, azúcar, cacao en polvo",
            Allergens = "en:gluten, en:milk",
            ExternalCategories = "Snacks dulces, Galletas",
            ImageUrl = "https://images.openfoodfacts.org/images/products/779/089/500/0997/front_es.jpg",
            NutriScore = "e",
            NovaGroup = 4
        };

        var secondResult = await _productAppService.SaveFromExternalAsync(secondInput);

        // Assert: Same Id, original non-null properties preserved, null metadata enriched, single record in DB
        secondResult.ShouldNotBeNull();
        secondResult.Id.ShouldBe(firstResult.Id);
        secondResult.Barcode.ShouldBe("7790895000997");
        secondResult.Name.ShouldBe("Galletitas Dulces Rellenas Sabor Chocolate");
        secondResult.Category.ShouldBe(ItemCategory.Snacks);
        secondResult.Brand.ShouldBe("Bagley");
        secondResult.Quantity.ShouldBe("118 g");
        secondResult.Ingredients.ShouldBe("Harina de trigo enriquecida, azúcar, cacao en polvo");
        secondResult.Allergens.ShouldBe("en:gluten, en:milk");
        secondResult.ExternalCategories.ShouldBe("Snacks dulces, Galletas");
        secondResult.ImageUrl.ShouldBe("https://images.openfoodfacts.org/images/products/779/089/500/0997/front_es.jpg");
        secondResult.NutriScore.ShouldBe("E");
        secondResult.NovaGroup.ShouldBe(4);

        var countInDb = await WithUnitOfWorkAsync(async () =>
            await _productRepository.CountAsync(x => x.Barcode == "7790895000997"));
        countInDb.ShouldBe(1);

        // Act 3: Third external save when all metadata is already populated should keep everything untouched
        var thirdResult = await _productAppService.SaveFromExternalAsync(new SaveExternalProductDto
        {
            Barcode = "7790895000997",
            Name = "Otro Nombre",
            Category = ItemCategory.Beverages,
            Brand = "Otra Marca",
            Quantity = "500 g",
            Ingredients = "Otros ingredientes",
            Allergens = "en:peanuts",
            ExternalCategories = "Bebidas",
            ImageUrl = "https://images.openfoodfacts.org/other.jpg",
            NutriScore = "a",
            NovaGroup = 1
        });

        thirdResult.Id.ShouldBe(firstResult.Id);
        thirdResult.Name.ShouldBe("Galletitas Dulces Rellenas Sabor Chocolate");
        thirdResult.Category.ShouldBe(ItemCategory.Snacks);
        thirdResult.Brand.ShouldBe("Bagley");
        thirdResult.Quantity.ShouldBe("118 g");
        thirdResult.Ingredients.ShouldBe("Harina de trigo enriquecida, azúcar, cacao en polvo");
        thirdResult.Allergens.ShouldBe("en:gluten, en:milk");
        thirdResult.ExternalCategories.ShouldBe("Snacks dulces, Galletas");
        thirdResult.ImageUrl.ShouldBe("https://images.openfoodfacts.org/images/products/779/089/500/0997/front_es.jpg");
        thirdResult.NutriScore.ShouldBe("E");
        thirdResult.NovaGroup.ShouldBe(4);
    }

    [Theory]
    [InlineData(ItemCategory.Undefined, null, null, null)]
    [InlineData(ItemCategory.Snacks, "Z", null, null)]
    [InlineData(ItemCategory.Snacks, null, 5, null)]
    [InlineData(ItemCategory.Snacks, null, null, "ftp://example.com/img.jpg")]
    [InlineData(ItemCategory.Snacks, null, null, "not-a-url")]
    public async Task Should_Reject_Invalid_Dto_Fields_On_SaveFromExternalAsync(
        ItemCategory category,
        string? nutriScore,
        int? novaGroup,
        string? imageUrl)
    {
        var invalidInput = new SaveExternalProductDto
        {
            Barcode = "7790895000998",
            Name = "Producto Test",
            Category = category,
            NutriScore = nutriScore,
            NovaGroup = novaGroup,
            ImageUrl = imageUrl
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _productAppService.SaveFromExternalAsync(invalidInput);
        });
    }

    [Theory]
    [InlineData(ItemCategory.Undefined, null)]
    [InlineData(ItemCategory.Dairy, "ftp://example.com/img.jpg")]
    [InlineData(ItemCategory.Dairy, "not-a-url")]
    public async Task Should_Reject_Invalid_Category_Or_ImageUrl_On_CreateAsync(
        ItemCategory category,
        string? imageUrl)
    {
        var invalidInput = new CreateProductDto
        {
            Barcode = "7790895000999",
            Name = "Producto Test",
            Category = category,
            ImageUrl = imageUrl
        };

        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _productAppService.CreateAsync(invalidInput);
        });
    }
}

