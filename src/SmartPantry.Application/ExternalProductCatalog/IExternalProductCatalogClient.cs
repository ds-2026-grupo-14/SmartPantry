using System.Threading.Tasks;

namespace SmartPantry.ExternalProductCatalog;

public interface IExternalProductCatalogClient
{
    Task<ExternalProductDto?> GetByBarcodeAsync(string barcode);
}
