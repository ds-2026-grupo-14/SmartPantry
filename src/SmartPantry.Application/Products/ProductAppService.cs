using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SmartPantry.Products;

[Authorize]
public class ProductAppService :
    CrudAppService<
        Product,
        ProductDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateProductDto,
        UpdateProductDto>,
    IProductAppService
{
    public ProductAppService(IRepository<Product, Guid> repository)
        : base(repository)
    {
    }

    public override async Task<ProductDto> CreateAsync(CreateProductDto input)
    {
        var normalizedBarcode = input.Barcode.Trim();
        var existing = await Repository.FindAsync(x => x.Barcode == normalizedBarcode);
        if (existing is not null)
        {
            throw new BusinessException(SmartPantryDomainErrorCodes.DuplicateProductBarcode);
        }

        var product = new Product(
            GuidGenerator.Create(),
            normalizedBarcode,
            input.Name,
            input.Category,
            input.Brand,
            input.Quantity,
            input.Ingredients,
            input.Allergens,
            input.ExternalCategories,
            input.ImageUrl,
            input.NutriScore,
            input.NovaGroup
        );

        await Repository.InsertAsync(product, autoSave: true);

        return await MapToGetOutputDtoAsync(product);
    }

    // Sobreescribimos UpdateAsync para delegar la mutación a las reglas del dominio
    public override async Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto input)
    {
        var product = await Repository.GetAsync(id);

        product.Update(
            input.Name,
            input.Category,
            input.Brand,
            input.Quantity,
            input.Ingredients,
            input.Allergens,
            input.ExternalCategories,
            input.ImageUrl,
            input.NutriScore,
            input.NovaGroup
        );

        await Repository.UpdateAsync(product, autoSave: true);

        return await MapToGetOutputDtoAsync(product);
    }

    public async Task<ProductDto> SaveFromExternalAsync(SaveExternalProductDto input)
    {
        var normalizedBarcode = input.Barcode.Trim();
        var existing = await Repository.FindAsync(x => x.Barcode == normalizedBarcode);

        if (existing is null)
        {
            var product = new Product(
                GuidGenerator.Create(),
                normalizedBarcode,
                input.Name,
                input.Category,
                input.Brand,
                input.Quantity,
                input.Ingredients,
                input.Allergens,
                input.ExternalCategories,
                input.ImageUrl,
                input.NutriScore,
                input.NovaGroup
            );

            await Repository.InsertAsync(product, autoSave: true);
            return await MapToGetOutputDtoAsync(product);
        }

        var hasChanges =
            (existing.Brand is null && !string.IsNullOrWhiteSpace(input.Brand)) ||
            (existing.Quantity is null && !string.IsNullOrWhiteSpace(input.Quantity)) ||
            (existing.Ingredients is null && !string.IsNullOrWhiteSpace(input.Ingredients)) ||
            (existing.Allergens is null && !string.IsNullOrWhiteSpace(input.Allergens)) ||
            (existing.ExternalCategories is null && !string.IsNullOrWhiteSpace(input.ExternalCategories)) ||
            (existing.ImageUrl is null && !string.IsNullOrWhiteSpace(input.ImageUrl)) ||
            (existing.NutriScore is null && !string.IsNullOrWhiteSpace(input.NutriScore)) ||
            (!existing.NovaGroup.HasValue && input.NovaGroup.HasValue);

        if (hasChanges)
        {
            existing.Update(
                existing.Name,
                existing.Category,
                existing.Brand ?? input.Brand,
                existing.Quantity ?? input.Quantity,
                existing.Ingredients ?? input.Ingredients,
                existing.Allergens ?? input.Allergens,
                existing.ExternalCategories ?? input.ExternalCategories,
                existing.ImageUrl ?? input.ImageUrl,
                existing.NutriScore ?? input.NutriScore,
                existing.NovaGroup ?? input.NovaGroup
            );

            await Repository.UpdateAsync(existing, autoSave: true);
        }

        return await MapToGetOutputDtoAsync(existing);
    }
}