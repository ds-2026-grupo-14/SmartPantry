using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartPantry.OpenFoodFacts;

internal class OpenFoodFactsResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("status")]
    public JsonElement? Status { get; set; }

    [JsonPropertyName("product")]
    public OpenFoodFactsProduct? Product { get; set; }
}

internal class OpenFoodFactsSearchRequest
{
    [JsonPropertyName("q")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }

    [JsonPropertyName("fields")]
    public IReadOnlyList<string> Fields { get; set; } = [];
}

internal class OpenFoodFactsSearchResponse
{
    [JsonPropertyName("count")]
    public long Count { get; set; }

    [JsonPropertyName("hits")]
    public List<OpenFoodFactsProduct>? Hits { get; set; }
}

internal class OpenFoodFactsProduct
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("product_name")]
    public JsonElement? ProductName { get; set; }

    [JsonPropertyName("categories")]
    public JsonElement? Categories { get; set; }

    [JsonPropertyName("brands")]
    public JsonElement? Brands { get; set; }

    [JsonPropertyName("quantity")]
    public JsonElement? Quantity { get; set; }

    [JsonPropertyName("ingredients_text")]
    public JsonElement? IngredientsText { get; set; }

    [JsonPropertyName("allergens")]
    public JsonElement? Allergens { get; set; }

    [JsonPropertyName("image_front_url")]
    public string? ImageFrontUrl { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("nutriscore_grade")]
    public string? NutriScoreGrade { get; set; }

    [JsonPropertyName("nova_group")]
    public JsonElement? NovaGroup { get; set; }
}
