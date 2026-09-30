using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace SmartPantry.ExternalProductCatalog;

public interface IExternalProductCatalogAppService : IApplicationService
{
    Task<ExternalProductResultDto> GetByBarcodeAsync(GetExternalProductByBarcodeDto input);
}

