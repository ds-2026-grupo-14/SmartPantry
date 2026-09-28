using System.ComponentModel.DataAnnotations;

namespace SmartPantry.ExternalProductCatalog;

public class GetExternalProductByBarcodeDto
{
    [Required]
    [RegularExpression("^[0-9]{8,14}$")]
    public string Barcode { get; set; } = string.Empty;
}

