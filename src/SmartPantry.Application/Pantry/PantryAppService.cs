using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using SmartPantry.Products;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace SmartPantry.Pantry;

[Authorize]
public class PantryAppService : SmartPantryAppService, IPantryAppService
{
    private readonly IRepository<PantryItem, Guid> _pantryItemRepository;
    private readonly IRepository<Product, Guid> _productRepository;

    public PantryAppService(
        IRepository<PantryItem, Guid> pantryItemRepository,
        IRepository<Product, Guid> productRepository)
    {
        _pantryItemRepository = pantryItemRepository;
        _productRepository = productRepository;
    }

    public async Task<PantryItemDto> GetAsync(Guid id)
    {
        var item = await GetOwnedItemAsync(id);
        return await MapToDtoAsync(item);
    }

    public async Task<PagedResultDto<PantryItemDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var currentUserId = CurrentUser.GetId();
        var queryable = await _pantryItemRepository.GetQueryableAsync();
        var userQuery = queryable.Where(x => x.UserId == currentUserId);

        var totalCount = await AsyncExecuter.CountAsync(userQuery);

        var pagedQuery = userQuery
            .OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : input.Sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount);

        var items = await AsyncExecuter.ToListAsync(pagedQuery);

        var productIds = items.Select(x => x.ProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(x => productIds.Contains(x.Id));
        var productDict = products.ToDictionary(p => p.Id, p => p.Name);

        var dtos = items.Select(item => new PantryItemDto
        {
            Id = item.Id,
            UserId = item.UserId,
            ProductId = item.ProductId,
            ProductName = productDict.TryGetValue(item.ProductId, out var name) ? name : string.Empty,
            Quantity = item.Quantity,
            Unit = item.Unit,
            ExpirationDate = item.ExpirationDate,
            State = item.State,
            CreationTime = item.CreationTime,
            CreatorId = item.CreatorId,
            LastModificationTime = item.LastModificationTime,
            LastModifierId = item.LastModifierId
        }).ToList();

        return new PagedResultDto<PantryItemDto>(totalCount, dtos);
    }

    public async Task<PantryItemDto> CreateAsync(CreatePantryItemDto input)
    {
        var currentUserId = CurrentUser.GetId();
        var product = await _productRepository.GetAsync(input.ProductId);

        var item = new PantryItem(
            GuidGenerator.Create(),
            currentUserId,
            input.ProductId,
            input.ExpirationDate?.Date,
            PantryItemState.Active,
            input.Quantity,
            input.Unit
        );

        await _pantryItemRepository.InsertAsync(item, autoSave: true);

        return new PantryItemDto
        {
            Id = item.Id,
            UserId = item.UserId,
            ProductId = item.ProductId,
            ProductName = product.Name,
            Quantity = item.Quantity,
            Unit = item.Unit,
            ExpirationDate = item.ExpirationDate,
            State = item.State,
            CreationTime = item.CreationTime,
            CreatorId = item.CreatorId,
            LastModificationTime = item.LastModificationTime,
            LastModifierId = item.LastModifierId
        };
    }

    public async Task<PantryItemDto> UpdateAsync(Guid id, UpdatePantryItemDto input)
    {
        var item = await GetOwnedItemAsync(id);

        item.SetQuantity(input.Quantity);
        item.SetUnit(input.Unit);
        item.SetExpirationDate(input.ExpirationDate?.Date);

        await _pantryItemRepository.UpdateAsync(item, autoSave: true);

        return await MapToDtoAsync(item);
    }

    public async Task<PantryItemDto> ConsumeAsync(Guid id)
    {
        var item = await GetOwnedItemAsync(id);
        item.Consume();

        await _pantryItemRepository.UpdateAsync(item, autoSave: true);

        return await MapToDtoAsync(item);
    }

    public async Task DeleteAsync(Guid id)
    {
        var item = await GetOwnedItemAsync(id);
        await _pantryItemRepository.DeleteAsync(item, autoSave: true);
    }

    private async Task<PantryItem> GetOwnedItemAsync(Guid id)
    {
        var item = await _pantryItemRepository.GetAsync(id);
        var currentUserId = CurrentUser.GetId();

        if (item.UserId != currentUserId)
        {
            throw new AbpAuthorizationException("No tiene permisos para acceder o modificar un ítem de despensa de otro usuario.");
        }

        return item;
    }

    private async Task<PantryItemDto> MapToDtoAsync(PantryItem item)
    {
        var product = await _productRepository.FindAsync(item.ProductId);

        return new PantryItemDto
        {
            Id = item.Id,
            UserId = item.UserId,
            ProductId = item.ProductId,
            ProductName = product?.Name ?? string.Empty,
            Quantity = item.Quantity,
            Unit = item.Unit,
            ExpirationDate = item.ExpirationDate,
            State = item.State,
            CreationTime = item.CreationTime,
            CreatorId = item.CreatorId,
            LastModificationTime = item.LastModificationTime,
            LastModifierId = item.LastModifierId
        };
    }
}
