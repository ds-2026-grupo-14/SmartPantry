using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SmartPantry.Preferences;
using SmartPantry.Products;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace SmartPantry.ExternalProductCatalog;

[Authorize]
[RemoteService(IsEnabled = false)]
public class ExternalProductCatalogAppService : SmartPantryAppService, IExternalProductCatalogAppService
{
    private readonly IExternalProductCatalogClient _externalClient;
    private readonly IRepository<Product, Guid> _productRepository;

    public ExternalProductCatalogAppService(
        IExternalProductCatalogClient externalClient,
        IRepository<Product, Guid> productRepository)
    {
        _externalClient = externalClient;
        _productRepository = productRepository;
    }

    public async Task<ExternalProductResultDto> GetByBarcodeAsync(GetExternalProductByBarcodeDto input)
    {
        try
        {
            var product = await _externalClient.GetByBarcodeAsync(input.Barcode.Trim());

            if (product is not null)
            {
                await EnrichProductsAsync([product]);
            }

            return new ExternalProductResultDto
            {
                Status = product is not null ? ExternalProductQueryStatus.Found : ExternalProductQueryStatus.NotFound,
                Product = product
            };
        }
        catch (RateLimitExceededException)
        {
            return new ExternalProductResultDto { Status = ExternalProductQueryStatus.RateLimitExceeded };
        }
        catch (ServiceUnavailableException)
        {
            return new ExternalProductResultDto { Status = ExternalProductQueryStatus.ServiceUnavailable };
        }
    }

    public async Task<ExternalProductSearchResultDto> SearchAsync(SearchExternalProductsDto input)
    {
        try
        {
            var results = await _externalClient.SearchByTextAsync(
                input.Query.Trim(),
                input.SkipCount,
                input.MaxResultCount);

            if (results.Items.Count > 0)
            {
                await EnrichProductsAsync(results.Items);
            }

            return new ExternalProductSearchResultDto
            {
                Status = ExternalProductQueryStatus.Found,
                Results = results
            };
        }
        catch (RateLimitExceededException)
        {
            return new ExternalProductSearchResultDto
            {
                Status = ExternalProductQueryStatus.RateLimitExceeded,
                Results = new PagedResultDto<ExternalProductDto>(0, [])
            };
        }
        catch (ServiceUnavailableException)
        {
            return new ExternalProductSearchResultDto
            {
                Status = ExternalProductQueryStatus.ServiceUnavailable,
                Results = new PagedResultDto<ExternalProductDto>(0, [])
            };
        }
    }

    private async Task EnrichProductsAsync(IReadOnlyList<ExternalProductDto> products)
    {
        var barcodes = products
            .Where(product => !string.IsNullOrWhiteSpace(product.Barcode))
            .Select(product => product.Barcode.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var productIdsByBarcode = new Dictionary<string, Guid>(StringComparer.Ordinal);
        if (barcodes.Count > 0)
        {
            var existingProducts = await _productRepository.GetListAsync(x => barcodes.Contains(x.Barcode));
            foreach (var existingProduct in existingProducts)
            {
                productIdsByBarcode.TryAdd(existingProduct.Barcode, existingProduct.Id);
            }
        }

        foreach (var product in products)
        {
            var trimmedBarcode = product.Barcode?.Trim();
            product.InternalProductId = !string.IsNullOrEmpty(trimmedBarcode) &&
                                        productIdsByBarcode.TryGetValue(trimmedBarcode, out var internalProductId)
                ? internalProductId
                : null;

            product.DietaryEvaluation ??= new DietaryEvaluationDto();
            product.DietaryEvaluation.VerificationStatus = DietaryVerificationStatus.NotConfigured;
            product.DietaryEvaluation.MatchedTerms ??= [];
        }
    }
}
