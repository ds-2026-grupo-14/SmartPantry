using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using SmartPantry.ExternalProductCatalog;

namespace SmartPantry.OpenFoodFacts;

public class OpenFoodFactsProductCatalogClient : IExternalProductCatalogClient
{
    private readonly HttpClient _httpClient;

    public OpenFoodFactsProductCatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ExternalProductDto?> GetByBarcodeAsync(string barcode)
    {
        var response = await _httpClient.GetAsync($"api/v3/product/{barcode}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new RateLimitExceededException();
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable || response.StatusCode == HttpStatusCode.InternalServerError)
        {
            throw new ServiceUnavailableException();
        }

        response.EnsureSuccessStatusCode();

        var jsonResult = await response.Content.ReadFromJsonAsync<OpenFoodFactsResponse>();

        if (jsonResult?.Product == null)
            return null;

        return new ExternalProductDto
        {
            Barcode = barcode,
            Name = jsonResult.Product.ProductName,
            Brand = jsonResult.Product.Brands,
            Category = jsonResult.Product.Categories,
            Quantity = jsonResult.Product.Quantity,
            Ingredients = jsonResult.Product.IngredientsText,
            Allergens = jsonResult.Product.Allergens
        };
    }
}
