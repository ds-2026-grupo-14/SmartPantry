using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace SmartPantry.Pantry;

public interface IPantryAppService : IApplicationService
{
    Task<PantryItemDto> GetAsync(Guid id);

    Task<PagedResultDto<PantryItemDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<PantryItemDto> CreateAsync(CreatePantryItemDto input);

    Task<PantryItemDto> UpdateAsync(Guid id, UpdatePantryItemDto input);

    Task<PantryItemDto> ConsumeAsync(Guid id);

    Task DeleteAsync(Guid id);
}
