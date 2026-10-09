using System;
using System.Threading.Tasks;
using SmartPantry.Products;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace SmartPantry.Pantry;

public class PantryDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public static readonly Guid DefaultUserId = Guid.Parse("2e701e62-71ab-4da4-a476-407151370bb8");

    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<PantryItem, Guid> _pantryItemRepository;
    private readonly IGuidGenerator _guidGenerator;

    public PantryDataSeedContributor(
        IRepository<Product, Guid> productRepository,
        IRepository<PantryItem, Guid> pantryItemRepository,
        IGuidGenerator guidGenerator)
    {
        _productRepository = productRepository;
        _pantryItemRepository = pantryItemRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (await _pantryItemRepository.GetCountAsync() > 0)
        {
            return;
        }

        var product = await GetOrCreateSampleProductAsync();
        var today = DateTime.Today;

        // 1. Vencido (DateTime.Today.AddDays(-2))
        var expiredItem = new PantryItem(
            _guidGenerator.Create(),
            DefaultUserId,
            product.Id,
            today.AddDays(-2),
            PantryItemState.Active
        );

        // 2. Por vencer (DateTime.Today.AddDays(2))
        var expiringSoonItem = new PantryItem(
            _guidGenerator.Create(),
            DefaultUserId,
            product.Id,
            today.AddDays(2),
            PantryItemState.Active
        );

        // 3. Futuro lejano (DateTime.Today.AddMonths(6))
        var farFutureItem = new PantryItem(
            _guidGenerator.Create(),
            DefaultUserId,
            product.Id,
            today.AddMonths(6),
            PantryItemState.Active
        );

        // 4. Sin fecha (null)
        var noDateItem = new PantryItem(
            _guidGenerator.Create(),
            DefaultUserId,
            product.Id,
            null,
            PantryItemState.Active
        );

        // 5. Consumido (State = Consumed)
        var consumedItem = new PantryItem(
            _guidGenerator.Create(),
            DefaultUserId,
            product.Id,
            today.AddDays(10),
            PantryItemState.Consumed
        );

        await _pantryItemRepository.InsertAsync(expiredItem, autoSave: true);
        await _pantryItemRepository.InsertAsync(expiringSoonItem, autoSave: true);
        await _pantryItemRepository.InsertAsync(farFutureItem, autoSave: true);
        await _pantryItemRepository.InsertAsync(noDateItem, autoSave: true);
        await _pantryItemRepository.InsertAsync(consumedItem, autoSave: true);
    }

    private async Task<Product> GetOrCreateSampleProductAsync()
    {
        const string barcode = "7791234567890";
        var existingProduct = await _productRepository.FindAsync(x => x.Barcode == barcode);
        if (existingProduct is not null)
        {
            return existingProduct;
        }

        var newProduct = new Product(
            _guidGenerator.Create(),
            barcode,
            "Leche Entera Clásica",
            ItemCategory.Dairy,
            brand: "La Serenísima",
            quantity: "1 L"
        );

        return await _productRepository.InsertAsync(newProduct, autoSave: true);
    }
}
