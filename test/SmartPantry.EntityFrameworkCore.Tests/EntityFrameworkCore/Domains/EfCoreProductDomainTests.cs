using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SmartPantry.Products;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace SmartPantry.EntityFrameworkCore.Domains;

[Collection(SmartPantryTestConsts.CollectionDefinitionName)]
public class EfCoreProductDomainTests : ProductDomainTests<SmartPantryEntityFrameworkCoreTestModule>
{
    private readonly IRepository<Product, Guid> _productRepository;

    public EfCoreProductDomainTests()
    {
        _productRepository = GetRequiredService<IRepository<Product, Guid>>();
    }

    [Fact]
    public async Task Should_Persist_And_Retrieve_Product_Nutritional_Metadata()
    {
        var id = Guid.NewGuid();
        var product = new Product(
            id,
            "7791234567891",
            "Yogur Proteico",
            ItemCategory.Dairy,
            brand: "La Serenísima",
            quantity: "200g",
            ingredients: "Leche descremada, proteínas lácteas",
            allergens: "Lactosa",
            externalCategories: "Lácteos, Yogures",
            imageUrl: "https://images.openfoodfacts.org/yogur.jpg",
            nutriScore: "A",
            novaGroup: 1
        );

        await WithUnitOfWorkAsync(async () =>
        {
            await _productRepository.InsertAsync(product, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var entityInDb = await _productRepository.GetAsync(id);
            entityInDb.ShouldNotBeNull();
            entityInDb.Barcode.ShouldBe("7791234567891");
            entityInDb.ExternalCategories.ShouldBe("Lácteos, Yogures");
            entityInDb.ImageUrl.ShouldBe("https://images.openfoodfacts.org/yogur.jpg");
            entityInDb.NutriScore.ShouldBe("A");
            entityInDb.NovaGroup.ShouldBe(1);
        });
    }

    [Fact]
    public async Task Should_Persist_Null_Nutritional_Metadata_When_Omitted()
    {
        var id = Guid.NewGuid();
        var product = new Product(
            id,
            "7791234567892",
            "Manzanas Rojas",
            ItemCategory.FruitsAndVegetables
        );

        await WithUnitOfWorkAsync(async () =>
        {
            await _productRepository.InsertAsync(product, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var entityInDb = await _productRepository.GetAsync(id);
            entityInDb.ShouldNotBeNull();
            entityInDb.ExternalCategories.ShouldBeNull();
            entityInDb.ImageUrl.ShouldBeNull();
            entityInDb.NutriScore.ShouldBeNull();
            entityInDb.NovaGroup.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Should_Enforce_Unique_Barcode_Constraint()
    {
        var product1 = new Product(
            Guid.NewGuid(),
            "7791234567893",
            "Producto Original",
            ItemCategory.DryGoods
        );

        var product2 = new Product(
            Guid.NewGuid(),
            "7791234567893",
            "Producto Duplicado",
            ItemCategory.Beverages
        );

        await WithUnitOfWorkAsync(async () =>
        {
            await _productRepository.InsertAsync(product1, autoSave: true);
        });

        await Should.ThrowAsync<DbUpdateException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await _productRepository.InsertAsync(product2, autoSave: true);
            });
        });
    }
}

