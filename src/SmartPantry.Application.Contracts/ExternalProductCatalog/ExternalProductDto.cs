using System;

namespace SmartPantry.ExternalProductCatalog;

public class ExternalProductDto
{
    public string Barcode { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string? Quantity { get; set; }
    public string? Ingredients { get; set; }
    public string? Allergens { get; set; }
    public string? ImageUrl { get; set; }
    public string? NutriScore { get; set; }
    public int? NovaGroup { get; set; }
    public Guid? InternalProductId { get; set; }
    public DietaryEvaluationDto DietaryEvaluation { get; set; } = new();
}

