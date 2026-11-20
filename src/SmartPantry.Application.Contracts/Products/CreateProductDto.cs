using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartPantry.Products;

public class CreateProductDto : IValidatableObject
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