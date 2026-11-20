using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SmartPantry.ExternalProductCatalog;
using SmartPantry.Preferences;
using SmartPantry.Products;
using Volo.Abp.Application.Dtos;

namespace SmartPantry.OpenFoodFacts;

public class OpenFoodFactsProductCatalogClient : IExternalProductCatalogClient
{
    private static readonly string[] RequestedFields =
    [
        "code",
        "product_name",
        "brands",
        "categories",
        "quantity",
        "ingredients_text",
        "allergens",
        "image_front_url",
        "nutriscore_grade",
        "nova_group"
    ];

    private readonly HttpClient _httpClient;
    private readonly OpenFoodFactsOptions _options;

    public OpenFoodFactsProductCatalogClient(
        HttpClient httpClient,
        IOptions<OpenFoodFactsOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ExternalProductDto?> GetByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        var normalizedBarcode = barcode.Trim();
        var fieldsQuery = string.Join(",", RequestedFields);
        var url = $"api/v3/product/{Uri.EscapeDataString(normalizedBarcode)}?fields={fieldsQuery}";

        var jsonResult = await SendAndReadAsync<OpenFoodFactsResponse>(
            () => _httpClient.GetAsync(url),
            allowNotFound: true);

        if (jsonResult?.Product is null)
        {
            return null;
        }

        return MapToExternalProductDto(jsonResult.Product, normalizedBarcode);
    }

    public async Task<PagedResultDto<ExternalProductDto>> SearchByTextAsync(
        string query,
        int skipCount,
        int maxResultCount)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new PagedResultDto<ExternalProductDto>(0, []);
        }

        var safeMaxResultCount = maxResultCount <= 0 ? 10 : maxResultCount;
        var safeSkipCount = skipCount < 0 ? 0 : skipCount;
        var page = (safeSkipCount / safeMaxResultCount) + 1;

        var searchBaseUrl = string.IsNullOrWhiteSpace(_options.SearchBaseUrl)
            ? "https://search.openfoodfacts.org/"
            : _options.SearchBaseUrl.Trim();
        var normalizedBaseUrl = searchBaseUrl.EndsWith('/') ? searchBaseUrl : $"{searchBaseUrl}/";
        var searchUri = new Uri(new Uri(normalizedBaseUrl), "search");

        var request = new OpenFoodFactsSearchRequest
        {
            Query = query.Trim(),
            Page = page,
            PageSize = safeMaxResultCount,
            Fields = RequestedFields
        };

        var jsonResult = await SendAndReadAsync<OpenFoodFactsSearchResponse>(
            () => _httpClient.PostAsJsonAsync(searchUri, request),
            allowNotFound: false);

        if (jsonResult?.Hits is null || jsonResult.Hits.Count == 0)
        {
            return new PagedResultDto<ExternalProductDto>(0, []);
        }

        var items = new List<ExternalProductDto>(jsonResult.Hits.Count);
        foreach (var hit in jsonResult.Hits)
        {
            var mapped = MapToExternalProductDto(hit, fallbackBarcode: null);
            if (mapped is not null)
            {
                items.Add(mapped);
            }
        }

        if (items.Count == 0)
        {
            return new PagedResultDto<ExternalProductDto>(0, []);
        }

        return new PagedResultDto<ExternalProductDto>(Math.Max(jsonResult.Count, items.Count), items);
    }

    private static async Task<TResponse?> SendAndReadAsync<TResponse>(
        Func<Task<HttpResponseMessage>> sendAsync,
        bool allowNotFound)
    {
        try
        {
            using var response = await sendAsync();

            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitExceededException();
            }

            if ((int)response.StatusCode >= 500)
            {
                throw new ServiceUnavailableException();
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<TResponse>();
        }
        catch (RateLimitExceededException)
        {
            throw;
        }
        catch (ServiceUnavailableException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            throw new ServiceUnavailableException();
        }
        catch (TaskCanceledException)
        {
            throw new ServiceUnavailableException();
        }
        catch (JsonException)
        {
            throw new ServiceUnavailableException();
        }
    }

    private static ExternalProductDto? MapToExternalProductDto(
        OpenFoodFactsProduct source,
        string? fallbackBarcode)
    {
        var barcode = NormalizeBarcode(source.Code, fallbackBarcode);
        if (barcode is null)
        {
            return null;
        }

        var rawIngredients = ExtractOptionalText(source.IngredientsText);
        var ingredients = rawIngredients is not null && rawIngredients.Length > ProductConsts.MaxIngredientsLength
            ? rawIngredients[..ProductConsts.MaxIngredientsLength]
            : rawIngredients;

        return new ExternalProductDto
        {
            Barcode = barcode,
            Name = ExtractOptionalText(source.ProductName),
            Brand = ExtractOptionalText(source.Brands),
            Category = ExtractOptionalText(source.Categories),
            Quantity = ExtractOptionalText(source.Quantity),
            Ingredients = ingredients,
            Allergens = ExtractOptionalText(source.Allergens),
            ImageUrl = NormalizeImageUrl(source.ImageFrontUrl, source.ImageUrl),
            NutriScore = NormalizeNutriScore(source.NutriScoreGrade),
            NovaGroup = ParseNovaGroup(source.NovaGroup),
            InternalProductId = null,
            DietaryEvaluation = new DietaryEvaluationDto
            {
                VerificationStatus = DietaryVerificationStatus.NotConfigured
            }
        };
    }

    private static string? NormalizeBarcode(string? code, string? fallbackBarcode)
    {
        var candidate = string.IsNullOrWhiteSpace(code) ? fallbackBarcode : code;
        var trimmed = candidate?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > ProductConsts.MaxBarcodeLength
            ? trimmed[..ProductConsts.MaxBarcodeLength]
            : trimmed;
    }

    private static string? ExtractOptionalText(JsonElement? element)
    {
        if (!element.HasValue || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var value = element.Value;
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var text = value.GetString()?.Trim();
                return string.IsNullOrEmpty(text) ? null : text;

            case JsonValueKind.Array:
                var parts = new List<string>();
                foreach (var item in value.EnumerateArray())
                {
                    var part = item.ValueKind == JsonValueKind.String
                        ? item.GetString()?.Trim()
                        : item.ToString().Trim();
                    if (!string.IsNullOrEmpty(part))
                    {
                        parts.Add(part);
                    }
                }

                return parts.Count == 0 ? null : string.Join(", ", parts);

            default:
                var fallback = value.ToString().Trim();
                return string.IsNullOrEmpty(fallback) ? null : fallback;
        }
    }

    private static string? NormalizeImageUrl(string? imageFrontUrl, string? imageUrlFallback)
    {
        var candidate = !string.IsNullOrWhiteSpace(imageFrontUrl) ? imageFrontUrl : imageUrlFallback;
        var trimmed = candidate?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > ProductConsts.MaxImageUrlLength)
        {
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return trimmed;
    }

    private static string? NormalizeNutriScore(string? nutriScore)
    {
        var normalized = nutriScore?.Trim().ToUpperInvariant();
        return normalized is ("A" or "B" or "C" or "D" or "E") ? normalized : null;
    }

    private static int? ParseNovaGroup(JsonElement? element)
    {
        if (!element.HasValue || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        int parsed;
        if (element.Value.ValueKind == JsonValueKind.Number)
        {
            if (!element.Value.TryGetInt32(out parsed))
            {
                return null;
            }
        }
        else if (element.Value.ValueKind == JsonValueKind.String)
        {
            if (!int.TryParse(element.Value.GetString()?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return null;
            }
        }
        else
        {
            return null;
        }

        return parsed >= ProductConsts.MinNovaGroup && parsed <= ProductConsts.MaxNovaGroup
            ? parsed
            : null;
    }
}
