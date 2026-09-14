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
        string? allergens = null) : base(id)
    {
        SetBarcode(barcode);
        SetName(name);
        SetCategory(category);
        SetBrand(brand);
        SetQuantity(quantity);
        SetIngredients(ingredients);
        SetAllergens(allergens);
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
}