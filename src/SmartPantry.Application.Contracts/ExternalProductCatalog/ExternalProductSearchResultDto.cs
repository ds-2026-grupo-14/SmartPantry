using Volo.Abp.Application.Dtos;

namespace SmartPantry.ExternalProductCatalog;

public class ExternalProductSearchResultDto
{
    public ExternalProductQueryStatus Status { get; set; }
    public PagedResultDto<ExternalProductDto> Results { get; set; } = new(0, []);
}
