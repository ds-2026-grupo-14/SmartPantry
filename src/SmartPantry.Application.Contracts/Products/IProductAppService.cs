using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SmartPantry.Products;

public interface IProductAppService :
    ICrudAppService<
        
        ProductDto,
        Guid,
        PagedAndSortedResultRequestDto,
        CreateProductDto,
        UpdateProductDto>
{
}