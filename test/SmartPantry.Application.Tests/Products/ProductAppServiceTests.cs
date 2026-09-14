using System;
using System.Threading.Tasks;
using Shouldly;
using SmartPantry.Products;
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

        // Act
        var createdDto = await _productAppService.CreateAsync(input);

        // Assert - Comprobar que fue creado
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
}

