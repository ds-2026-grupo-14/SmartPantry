using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPantry.Controllers;
using Volo.Abp;

namespace SmartPantry.ExternalProductCatalog;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/app/external-product-catalog")]
[Authorize]
public class ExternalProductCatalogController : SmartPantryController, IExternalProductCatalogAppService
{
    private readonly IExternalProductCatalogAppService _appService;

    public ExternalProductCatalogController(IExternalProductCatalogAppService appService)
    {
        _appService = appService;
    }

    [HttpGet("by-barcode")]
    public Task<ExternalProductResultDto> GetByBarcodeAsync([FromQuery] GetExternalProductByBarcodeDto input)
        => _appService.GetByBarcodeAsync(input);

    [HttpGet("search")]
    public Task<ExternalProductSearchResultDto> SearchAsync([FromQuery] SearchExternalProductsDto input)
        => _appService.SearchAsync(input);
}
