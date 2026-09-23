using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace SmartPantry.Products;

[AllowAnonymous]
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
        var product = new Product(
            GuidGenerator.Create(),
            input.Barcode,
            input.Name,
            input.Category,
            input.Brand,
            input.Quantity,
            input.Ingredients,
            input.Allergens
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
            input.Allergens
        );

        await Repository.UpdateAsync(product, autoSave: true);

        return await MapToGetOutputDtoAsync(product);
    }

}