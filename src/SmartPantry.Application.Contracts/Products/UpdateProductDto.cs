using System.ComponentModel.DataAnnotations;

namespace SmartPantry.Products;

public class UpdateProductDto
{
    [Required]
    [StringLength(ProductConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ItemCategory Category { get; set; }

    [StringLength(ProductConsts.MaxBrandLength)]
    public string? Brand { get; set; }

    [StringLength(ProductConsts.MaxQuantityLength)]
    public string? Quantity { get; set; }

    [StringLength(ProductConsts.MaxIngredientsLength)]
    public string? Ingredients { get; set; }

    [StringLength(ProductConsts.MaxAllergensLength)]
    public string? Allergens { get; set; }
}

