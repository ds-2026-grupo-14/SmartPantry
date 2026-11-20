using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace SmartPantry.ExternalProductCatalog;

public interface IExternalProductCatalogClient
{
    Task<ExternalProductDto?> GetByBarcodeAsync(string barcode);
    Task<PagedResultDto<ExternalProductDto>> SearchByTextAsync(string query, int skipCount, int maxResultCount);
}
