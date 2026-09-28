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
}

