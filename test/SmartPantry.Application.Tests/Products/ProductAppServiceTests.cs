using System;
using System.Threading.Tasks;
using Shouldly;
using SmartPantry.Products;
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
            Allergens = "Ninguno"
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
            Allergens = "Puede contener trazas de soja"
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
}

