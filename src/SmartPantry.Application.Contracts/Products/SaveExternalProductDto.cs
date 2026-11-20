using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartPantry.Products;

public class SaveExternalProductDto : IValidatableObject
{
    [Required]
    [StringLength(ProductConsts.MaxBarcodeLength)]
    public string Barcode { get; set; } = string.Empty;

    [Required]
    [StringLength(ProductConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Range((int)ItemCategory.Dairy, (int)ItemCategory.Other)]
    public ItemCategory Category { get; set; }

    [StringLength(ProductConsts.MaxBrandLength)]
    public string? Brand { get; set; }

    [StringLength(ProductConsts.MaxQuantityLength)]
    public string? Quantity { get; set; }

    [StringLength(ProductConsts.MaxIngredientsLength)]
    public string? Ingredients { get; set; }

    [StringLength(ProductConsts.MaxAllergensLength)]
    public string? Allergens { get; set; }

    [StringLength(ProductConsts.MaxExternalCategoriesLength)]
    public string? ExternalCategories { get; set; }

    [StringLength(ProductConsts.MaxImageUrlLength)]
    public string? ImageUrl { get; set; }

    [StringLength(ProductConsts.MaxNutriScoreLength)]
    [RegularExpression("^[A-Ea-e]$")]
    public string? NutriScore { get; set; }

    [Range(ProductConsts.MinNovaGroup, ProductConsts.MaxNovaGroup)]
    public int? NovaGroup { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => ProductDtoValidation.ValidateImageUrl(ImageUrl);
}

internal static class ProductDtoValidation
{
    internal static IEnumerable<ValidationResult> ValidateImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            yield break;
        }

        var normalized = imageUrl.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            yield return new ValidationResult(
                "La URL de la imagen debe ser una URI absoluta válida con esquema http o https.",
                new[] { nameof(SaveExternalProductDto.ImageUrl) });
        }
    }
}
