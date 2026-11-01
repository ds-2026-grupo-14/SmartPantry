using System;
using System.Threading.Tasks;
using SmartPantry.Products;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace SmartPantry.Pantry;

public class PantryDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public static readonly Guid DefaultUserId = Guid.Parse("2e701e62-71ab-4da4-a476-407151370bb8");

    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<PantryItem, Guid> _pantryItemRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IdentityUserManager _userManager;

    public PantryDataSeedContributor(
        IRepository<Product, Guid> productRepository,
        IRepository<PantryItem, Guid> pantryItemRepository,
        IGuidGenerator guidGenerator,
        IdentityUserManager userManager)
    {
        _productRepository = productRepository;
        _pantryItemRepository = pantryItemRepository;
        _guidGenerator = guidGenerator;
        _userManager = userManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        var product = await GetOrCreateSampleProductAsync("7791234567890", "Leche Entera Clásica", ItemCategory.Dairy, "La Serenísima", "1 L");
        var today = DateTime.Today;

        // 1. Sembrado para DefaultUserId (compatibilidad histórica)
        if (await _pantryItemRepository.GetCountAsync() == 0)
        {
            var expiredItem = new PantryItem(
                _guidGenerator.Create(),
                DefaultUserId,
                product.Id,
                today.AddDays(-2),
                PantryItemState.Active,
                quantity: 1m,
                unit: "unidad"
            );

            var expiringSoonItem = new PantryItem(
                _guidGenerator.Create(),
                DefaultUserId,
                product.Id,
                today.AddDays(2),
                PantryItemState.Active,
                quantity: 2m,
                unit: "litros"
            );

            var farFutureItem = new PantryItem(
                _guidGenerator.Create(),
                DefaultUserId,
                product.Id,
                today.AddMonths(6),
                PantryItemState.Active,
                quantity: 5m,
                unit: "unidades"
            );

            var noDateItem = new PantryItem(
                _guidGenerator.Create(),
                DefaultUserId,
                product.Id,
                null,
                PantryItemState.Active,
                quantity: null,
                unit: null
            );

            var consumedItem = new PantryItem(
                _guidGenerator.Create(),
                DefaultUserId,
                product.Id,
                today.AddDays(10),
                PantryItemState.Consumed,
                quantity: 1m,
                unit: "botella"
            );

            await _pantryItemRepository.InsertAsync(expiredItem, autoSave: true);
            await _pantryItemRepository.InsertAsync(expiringSoonItem, autoSave: true);
            await _pantryItemRepository.InsertAsync(farFutureItem, autoSave: true);
            await _pantryItemRepository.InsertAsync(noDateItem, autoSave: true);
            await _pantryItemRepository.InsertAsync(consumedItem, autoSave: true);
        }

        // 2. Sembrado específico para el usuario admin
        var adminUser = await _userManager.FindByNameAsync("admin");
        if (adminUser != null)
        {
            var adminItemExists = await _pantryItemRepository.FindAsync(x => x.UserId == adminUser.Id);
            if (adminItemExists == null)
            {
                var arroz = await GetOrCreateSampleProductAsync("7799999000001", "Arroz Integral", ItemCategory.DryGoods, "Gallo", "1 kg");
                var yogur = await GetOrCreateSampleProductAsync("7799999000002", "Yogur Frutilla", ItemCategory.Dairy, "Yogs", "200 g");
                var fideos = await GetOrCreateSampleProductAsync("7799999000003", "Fideos Tallarines", ItemCategory.DryGoods, "Matarazzo", "500 g");

                await _pantryItemRepository.InsertAsync(new PantryItem(
                    _guidGenerator.Create(),
                    adminUser.Id,
                    product.Id,
                    today.AddDays(4),
                    PantryItemState.Active,
                    quantity: 2m,
                    unit: "litros"
                ), autoSave: true);

                await _pantryItemRepository.InsertAsync(new PantryItem(
                    _guidGenerator.Create(),
                    adminUser.Id,
                    arroz.Id,
                    today.AddMonths(5),
                    PantryItemState.Active,
                    quantity: 1.5m,
                    unit: "kg"
                ), autoSave: true);

                // Ítem con datos faltantes (RF-09)
                await _pantryItemRepository.InsertAsync(new PantryItem(
                    _guidGenerator.Create(),
                    adminUser.Id,
                    yogur.Id,
                    null,
                    PantryItemState.Active,
                    quantity: null,
                    unit: null
                ), autoSave: true);

                // Ítem consumido (RN-6)
                await _pantryItemRepository.InsertAsync(new PantryItem(
                    _guidGenerator.Create(),
                    adminUser.Id,
                    fideos.Id,
                    today.AddDays(-2),
                    PantryItemState.Consumed,
                    quantity: 500m,
                    unit: "g"
                ), autoSave: true);
            }
        }
    }

    private async Task<Product> GetOrCreateSampleProductAsync(
        string barcode,
        string name,
        ItemCategory category,
        string brand,
        string quantity)
    {
        var existingProduct = await _productRepository.FindAsync(x => x.Barcode == barcode);
        if (existingProduct is not null)
        {
            return existingProduct;
        }

        var newProduct = new Product(
            _guidGenerator.Create(),
            barcode,
            name,
            category,
            brand: brand,
            quantity: quantity
        );

        return await _productRepository.InsertAsync(newProduct, autoSave: true);
    }
}
