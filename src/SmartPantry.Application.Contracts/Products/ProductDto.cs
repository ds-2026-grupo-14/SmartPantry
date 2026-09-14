using System;
using Volo.Abp.Application.Dtos;

namespace SmartPantry.Products;

public class ProductDto : EntityDto<Guid>
{
    public string Barcode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ItemCategory Category { get; set; }
    public string? Brand { get; set; }
    public string? Quantity { get; set; }
    public string? Ingredients { get; set; }
    public string? Allergens { get; set; }
}