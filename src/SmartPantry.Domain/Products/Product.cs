using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace SmartPantry.Products;

public class Product : BasicAggregateRoot<Guid>
{
    public string Barcode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public ItemCategory Category { get; private set; }
    public string? Brand { get; private set; }
    public string? Quantity { get; private set; }
    public string? Ingredients { get; private set; }
    public string? Allergens { get; private set; }
    public string? ExternalCategories { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? NutriScore { get; private set; }
    public int? NovaGroup { get; private set; }

    // Constructor sin parámetros requerido por Entity Framework Core
    protected Product()
    {
    }

    // Constructor de dominio: protege las invariantes y normaliza los datos
    public Product(
        Guid id,
        string barcode,
        string name,
        ItemCategory category,
        string? brand = null,
        string? quantity = null,
        string? ingredients = null,
        string? allergens = null,
        string? externalCategories = null,
        string? imageUrl = null,
        string? nutriScore = null,
        int? novaGroup = null) : base(id)
    {
        SetBarcode(barcode);
        SetName(name);
        SetCategory(category);
        SetBrand(brand);
        SetQuantity(quantity);
        SetIngredients(ingredients);
        SetAllergens(allergens);
        SetNutritionalMetadata(imageUrl, nutriScore, novaGroup, externalCategories);
    }

    public void Update(
        string name,
        ItemCategory category,
        string? brand = null,
        string? quantity = null,
        string? ingredients = null,
        string? allergens = null,
        string? externalCategories = null,
        string? imageUrl = null,
        string? nutriScore = null,
        int? novaGroup = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del producto no puede ser nulo o vacío.", nameof(name));
        }

        var normalizedName = name.Trim();
        if (normalizedName.Length > ProductConsts.MaxNameLength)
        {
            throw new ArgumentException($"El nombre no puede superar los {ProductConsts.MaxNameLength} caracteres.", nameof(name));
        }

        if (category == ItemCategory.Undefined || !Enum.IsDefined(typeof(ItemCategory), category))
        {
            throw new ArgumentException("La categoría especificada no es válida.", nameof(category));
        }

        var normalizedBrand = brand?.Trim();
        if (!string.IsNullOrEmpty(normalizedBrand) && normalizedBrand.Length > ProductConsts.MaxBrandLength)
        {
            throw new ArgumentException($"La marca no puede superar los {ProductConsts.MaxBrandLength} caracteres.", nameof(brand));
        }

        var normalizedQuantity = quantity?.Trim();
        if (!string.IsNullOrEmpty(normalizedQuantity) && normalizedQuantity.Length > ProductConsts.MaxQuantityLength)
        {
            throw new ArgumentException($"La cantidad no puede superar los {ProductConsts.MaxQuantityLength} caracteres.", nameof(quantity));
        }

        var normalizedIngredients = ingredients?.Trim();
        if (!string.IsNullOrEmpty(normalizedIngredients) && normalizedIngredients.Length > ProductConsts.MaxIngredientsLength)
        {
            throw new ArgumentException($"Los ingredientes no pueden superar los {ProductConsts.MaxIngredientsLength} caracteres.", nameof(ingredients));
        }

        var normalizedAllergens = allergens?.Trim();
        if (!string.IsNullOrEmpty(normalizedAllergens) && normalizedAllergens.Length > ProductConsts.MaxAllergensLength)
        {
            throw new ArgumentException($"Los alérgenos no pueden superar los {ProductConsts.MaxAllergensLength} caracteres.", nameof(allergens));
        }

        var normalizedExternalCategories = NormalizeAndValidateExternalCategories(externalCategories);
        var normalizedImageUrl = NormalizeAndValidateImageUrl(imageUrl);
        var normalizedNutriScore = NormalizeAndValidateNutriScore(nutriScore);
        ValidateNovaGroup(novaGroup);

        Name = normalizedName;
        Category = category;
        Brand = string.IsNullOrEmpty(normalizedBrand) ? null : normalizedBrand;
        Quantity = string.IsNullOrEmpty(normalizedQuantity) ? null : normalizedQuantity;
        Ingredients = string.IsNullOrEmpty(normalizedIngredients) ? null : normalizedIngredients;
        Allergens = string.IsNullOrEmpty(normalizedAllergens) ? null : normalizedAllergens;
        ExternalCategories = normalizedExternalCategories;
        ImageUrl = normalizedImageUrl;
        NutriScore = normalizedNutriScore;
        NovaGroup = novaGroup;
    }

    // Métodos para proteger el estado válido y normalizar texto
    public void SetBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            throw new ArgumentException("El código de barras no puede ser nulo o vacío.", nameof(barcode));
        }

        barcode = barcode.Trim();

        if (barcode.Length > ProductConsts.MaxBarcodeLength)
        {
            throw new ArgumentException($"El código de barras no puede superar los {ProductConsts.MaxBarcodeLength} caracteres.", nameof(barcode));
        }

        Barcode = barcode;
    }

    public void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del producto no puede ser nulo o vacío.", nameof(name));
        }

        name = name.Trim();

        if (name.Length > ProductConsts.MaxNameLength)
        {
            throw new ArgumentException($"El nombre no puede superar los {ProductConsts.MaxNameLength} caracteres.", nameof(name));
        }

        Name = name;
    }

    public void SetCategory(ItemCategory category)
    {
        if (category == ItemCategory.Undefined || !Enum.IsDefined(typeof(ItemCategory), category))
        {
            throw new ArgumentException("La categoría especificada no es válida.", nameof(category));
        }

        Category = category;
    }

    public void SetBrand(string? brand)
    {
        brand = brand?.Trim();

        if (!string.IsNullOrEmpty(brand) && brand.Length > ProductConsts.MaxBrandLength)
        {
            throw new ArgumentException($"La marca no puede superar los {ProductConsts.MaxBrandLength} caracteres.", nameof(brand));
        }

        Brand = string.IsNullOrEmpty(brand) ? null : brand;
    }

    public void SetQuantity(string? quantity)
    {
        quantity = quantity?.Trim();

        if (!string.IsNullOrEmpty(quantity) && quantity.Length > ProductConsts.MaxQuantityLength)
        {
            throw new ArgumentException($"La cantidad no puede superar los {ProductConsts.MaxQuantityLength} caracteres.", nameof(quantity));
        }

        Quantity = string.IsNullOrEmpty(quantity) ? null : quantity;
    }

    public void SetIngredients(string? ingredients)
    {
        ingredients = ingredients?.Trim();

        if (!string.IsNullOrEmpty(ingredients) && ingredients.Length > ProductConsts.MaxIngredientsLength)
        {
            throw new ArgumentException($"Los ingredientes no pueden superar los {ProductConsts.MaxIngredientsLength} caracteres.", nameof(ingredients));
        }

        Ingredients = string.IsNullOrEmpty(ingredients) ? null : ingredients;
    }

    public void SetAllergens(string? allergens)
    {
        allergens = allergens?.Trim();

        if (!string.IsNullOrEmpty(allergens) && allergens.Length > ProductConsts.MaxAllergensLength)
        {
            throw new ArgumentException($"Los alérgenos no pueden superar los {ProductConsts.MaxAllergensLength} caracteres.", nameof(allergens));
        }

        Allergens = string.IsNullOrEmpty(allergens) ? null : allergens;
    }

    public void SetExternalCategories(string? externalCategories)
    {
        ExternalCategories = NormalizeAndValidateExternalCategories(externalCategories);
    }

    public void SetImageUrl(string? imageUrl)
    {
        ImageUrl = NormalizeAndValidateImageUrl(imageUrl);
    }

    public void SetNutriScore(string? nutriScore)
    {
        NutriScore = NormalizeAndValidateNutriScore(nutriScore);
    }

    public void SetNovaGroup(int? novaGroup)
    {
        ValidateNovaGroup(novaGroup);
        NovaGroup = novaGroup;
    }

    public void SetNutritionalMetadata(string? imageUrl, string? nutriScore, int? novaGroup, string? externalCategories)
    {
        var normalizedExternalCategories = NormalizeAndValidateExternalCategories(externalCategories);
        var normalizedImageUrl = NormalizeAndValidateImageUrl(imageUrl);
        var normalizedNutriScore = NormalizeAndValidateNutriScore(nutriScore);
        ValidateNovaGroup(novaGroup);

        ExternalCategories = normalizedExternalCategories;
        ImageUrl = normalizedImageUrl;
        NutriScore = normalizedNutriScore;
        NovaGroup = novaGroup;
    }

    private static string? NormalizeAndValidateExternalCategories(string? externalCategories)
    {
        var normalized = externalCategories?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > ProductConsts.MaxExternalCategoriesLength)
        {
            throw new ArgumentException($"Las categorías externas no pueden superar los {ProductConsts.MaxExternalCategoriesLength} caracteres.", nameof(externalCategories));
        }

        return normalized;
    }

    private static string? NormalizeAndValidateImageUrl(string? imageUrl)
    {
        var normalized = imageUrl?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > ProductConsts.MaxImageUrlLength)
        {
            throw new ArgumentException($"La URL de la imagen no puede superar los {ProductConsts.MaxImageUrlLength} caracteres.", nameof(imageUrl));
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("La URL de la imagen debe ser una URI absoluta válida con esquema http o https.", nameof(imageUrl));
        }

        return normalized;
    }

    private static string? NormalizeAndValidateNutriScore(string? nutriScore)
    {
        var normalized = nutriScore?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized is not ("A" or "B" or "C" or "D" or "E"))
        {
            throw new ArgumentException("El Nutri-Score debe ser A, B, C, D o E.", nameof(nutriScore));
        }

        return normalized;
    }

    private static void ValidateNovaGroup(int? novaGroup)
    {
        if (novaGroup.HasValue && (novaGroup.Value < ProductConsts.MinNovaGroup || novaGroup.Value > ProductConsts.MaxNovaGroup))
        {
            throw new ArgumentException($"El grupo NOVA debe estar comprendido entre {ProductConsts.MinNovaGroup} y {ProductConsts.MaxNovaGroup}.", nameof(novaGroup));
        }
    }
}