using System.Threading.Tasks;

namespace SmartPantry.ExternalProductCatalog;

public class ExternalProductCatalogAppService : SmartPantryAppService, IExternalProductCatalogAppService
{
    private readonly IExternalProductCatalogClient _externalClient;

    public ExternalProductCatalogAppService(IExternalProductCatalogClient externalClient)
    {
        _externalClient = externalClient;
    }

    public async Task<ExternalProductResultDto> GetByBarcodeAsync(GetExternalProductByBarcodeDto input)
    {
        try
        {
            var product = await _externalClient.GetByBarcodeAsync(input.Barcode);
            
            return new ExternalProductResultDto
            {
                Status = product != null ? ExternalProductQueryStatus.Found : ExternalProductQueryStatus.NotFound,
                Product = product
            };
        }
        catch (RateLimitExceededException)
        {
            return new ExternalProductResultDto { Status = ExternalProductQueryStatus.RateLimitExceeded };
        }
        catch (ServiceUnavailableException)
        {
            return new ExternalProductResultDto { Status = ExternalProductQueryStatus.ServiceUnavailable };
        }
    }
}
