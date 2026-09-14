using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;

namespace SmartPantry.Products;

// Habilitado temporalmente acceso anonimo para verificar la operacion en Swagger segun TP05
[AllowAnonymous]
public class ProductAppService : SmartPantryAppService, IProductAppService
{
    private readonly IRepository<Product, Guid> _productRepository;

    public ProductAppService(IRepository<Product, Guid> productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto input)
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

        await _productRepository.InsertAsync(product);

        return ObjectMapper.Map<Product, ProductDto>(product);
    }

    public async Task<ProductDto> GetAsync(Guid id)
    {
        var product = await _productRepository.GetAsync(id);

        return ObjectMapper.Map<Product, ProductDto>(product);
    }
}

