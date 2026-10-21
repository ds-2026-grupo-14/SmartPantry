using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using SmartPantry.Products;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace SmartPantry.Pantry;

public abstract class PantryAppServiceTests<TStartupModule> : SmartPantryApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IPantryAppService _pantryAppService;
    private readonly IRepository<Product, Guid> _productRepository;
    private readonly IRepository<PantryItem, Guid> _pantryItemRepository;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected PantryAppServiceTests()
    {
        _pantryAppService = GetRequiredService<IPantryAppService>();
        _productRepository = GetRequiredService<IRepository<Product, Guid>>();
        _pantryItemRepository = GetRequiredService<IRepository<PantryItem, Guid>>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private async Task<Product> GetOrCreateSampleProductAsync()
    {
        var product = await _productRepository.FirstOrDefaultAsync();
        if (product == null)
        {
            product = await _productRepository.InsertAsync(new Product(
                Guid.NewGuid(),
                "7790000000001",
                "Producto Test",
                ItemCategory.Other,
                brand: "Marca Test"
            ), autoSave: true);
        }
        return product;
    }

    private IDisposable ChangeCurrentUser(Guid userId, string userName = "other_user")
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, userId.ToString()),
            new Claim(AbpClaimTypes.UserName, userName),
            new Claim(AbpClaimTypes.Email, $"{userName}@test.com")
        });

        return _currentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    [Fact]
    public async Task Should_Create_And_Get_Pantry_Item()
    {
        // Arrange
        var product = await GetOrCreateSampleProductAsync();
        var expiration = DateTime.Today.AddDays(15);

        var input = new CreatePantryItemDto
        {
            ProductId = product.Id,
            Quantity = 2.5m,
            Unit = "kg",
            ExpirationDate = expiration
        };

        // Act - Crear ítem en despensa
        var createdDto = await _pantryAppService.CreateAsync(input);

        // Assert creación
        createdDto.ShouldNotBeNull();
        createdDto.Id.ShouldNotBe(Guid.Empty);
        createdDto.ProductId.ShouldBe(product.Id);
        createdDto.ProductName.ShouldBe(product.Name);
        createdDto.Quantity.ShouldBe(2.5m);
        createdDto.Unit.ShouldBe("kg");
        createdDto.ExpirationDate.ShouldBe(expiration.Date);
        createdDto.State.ShouldBe(PantryItemState.Active);

        // Act - Recuperar por ID
        var retrievedDto = await _pantryAppService.GetAsync(createdDto.Id);

        // Assert recuperación
        retrievedDto.ShouldNotBeNull();
        retrievedDto.Id.ShouldBe(createdDto.Id);
        retrievedDto.ProductName.ShouldBe(product.Name);
        retrievedDto.Quantity.ShouldBe(2.5m);
        retrievedDto.Unit.ShouldBe("kg");
        retrievedDto.ExpirationDate.ShouldBe(expiration.Date);
        retrievedDto.State.ShouldBe(PantryItemState.Active);
    }

    [Fact]
    public async Task Should_Get_Only_Current_User_Items_In_GetList()
    {
        // Arrange
        var product = await GetOrCreateSampleProductAsync();

        // 1. Usuario A crea un ítem
        var itemA = await _pantryAppService.CreateAsync(new CreatePantryItemDto
        {
            ProductId = product.Id,
            Quantity = 1m,
            Unit = "unidad"
        });

        var userBId = Guid.NewGuid();

        // 2. Cambiar a Usuario B y crear otro ítem
        using (ChangeCurrentUser(userBId, "user_b"))
        {
            var itemB = await _pantryAppService.CreateAsync(new CreatePantryItemDto
            {
                ProductId = product.Id,
                Quantity = 3m,
                Unit = "litros"
            });

            // Act - Listado para Usuario B
            var listUserB = await _pantryAppService.GetListAsync(new PagedAndSortedResultRequestDto
            {
                MaxResultCount = 50,
                SkipCount = 0
            });

            // Assert - Usuario B solo ve su propio ítem
            listUserB.Items.ShouldContain(x => x.Id == itemB.Id);
            listUserB.Items.ShouldNotContain(x => x.Id == itemA.Id);
        }

        // 3. Volver a Usuario A y verificar que no ve el ítem de B
        var listUserA = await _pantryAppService.GetListAsync(new PagedAndSortedResultRequestDto
        {
            MaxResultCount = 50,
            SkipCount = 0
        });

        listUserA.Items.ShouldContain(x => x.Id == itemA.Id);
        listUserA.Items.ShouldNotContain(x => x.Id != itemA.Id && x.UserId == userBId);
    }

    [Fact]
    public async Task Should_Update_Pantry_Item()
    {
        // Arrange
        var product = await GetOrCreateSampleProductAsync();
        var created = await _pantryAppService.CreateAsync(new CreatePantryItemDto
        {
            ProductId = product.Id,
            Quantity = 1m,
            Unit = "paquete",
            ExpirationDate = DateTime.Today.AddDays(5)
        });

        var newExpiration = DateTime.Today.AddDays(20);
        var updateInput = new UpdatePantryItemDto
        {
            Quantity = 4.5m,
            Unit = "kg",
            ExpirationDate = newExpiration
        };

        // Act
        var updated = await _pantryAppService.UpdateAsync(created.Id, updateInput);

        // Assert
        updated.ShouldNotBeNull();
        updated.Id.ShouldBe(created.Id);
        updated.Quantity.ShouldBe(4.5m);
        updated.Unit.ShouldBe("kg");
        updated.ExpirationDate.ShouldBe(newExpiration.Date);

        // Verificación en BD
        var inDb = await _pantryAppService.GetAsync(created.Id);
        inDb.Quantity.ShouldBe(4.5m);
        inDb.Unit.ShouldBe("kg");
        inDb.ExpirationDate.ShouldBe(newExpiration.Date);
    }

    [Fact]
    public async Task Should_Consume_Pantry_Item_Without_Deleting()
    {
        // Arrange
        var product = await GetOrCreateSampleProductAsync();
        var created = await _pantryAppService.CreateAsync(new CreatePantryItemDto
        {
            ProductId = product.Id,
            Quantity = 2m,
            Unit = "botellas"
        });

        // Act - Marcar como consumido
        var consumed = await _pantryAppService.ConsumeAsync(created.Id);

        // Assert - Estado cambiado a Consumed
        consumed.ShouldNotBeNull();
        consumed.State.ShouldBe(PantryItemState.Consumed);

        // Assert - Regla de Negocio 6: El ítem NO se elimina de la base de datos
        var entityInDb = await _pantryItemRepository.FindAsync(created.Id);
        entityInDb.ShouldNotBeNull();
        entityInDb.State.ShouldBe(PantryItemState.Consumed);
    }

    [Fact]
    public async Task Should_Delete_Owned_Pantry_Item()
    {
        // Arrange
        var product = await GetOrCreateSampleProductAsync();
        var created = await _pantryAppService.CreateAsync(new CreatePantryItemDto
        {
            ProductId = product.Id,
            Quantity = 1m,
            Unit = "lata"
        });

        // Act - Eliminar ítem
        await _pantryAppService.DeleteAsync(created.Id);

        // Assert - El ítem ya no existe en el repositorio
        var entityInDb = await _pantryItemRepository.FindAsync(created.Id);
        entityInDb.ShouldBeNull();

        // Assert - Intentar GetAsync arroja EntityNotFoundException
        await Should.ThrowAsync<EntityNotFoundException>(async () =>
        {
            await _pantryAppService.GetAsync(created.Id);
        });
    }

    [Fact]
    public async Task Should_Not_Allow_Negative_Quantity()
    {
        // Arrange
        var product = await GetOrCreateSampleProductAsync();
        var invalidInput = new CreatePantryItemDto
        {
            ProductId = product.Id,
            Quantity = -5m,
            Unit = "kg"
        };

        // Act & Assert - Rechazo por validación con AbpValidationException
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _pantryAppService.CreateAsync(invalidInput);
        });
    }

    [Fact]
    public async Task Should_Deny_Access_To_Other_User_Item()
    {
        // Arrange - Usuario A crea un ítem
        var product = await GetOrCreateSampleProductAsync();
        var itemA = await _pantryAppService.CreateAsync(new CreatePantryItemDto
        {
            ProductId = product.Id,
            Quantity = 2m,
            Unit = "unidades"
        });

        var userBId = Guid.NewGuid();

        // Act & Assert - Usuario B intenta acceder/modificar/consumir/eliminar el ítem de Usuario A
        using (ChangeCurrentUser(userBId, "user_b"))
        {
            // 1. GetAsync debe ser denegado (403 Forbidden)
            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _pantryAppService.GetAsync(itemA.Id);
            });

            // 2. UpdateAsync debe ser denegado (403 Forbidden)
            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _pantryAppService.UpdateAsync(itemA.Id, new UpdatePantryItemDto
                {
                    Quantity = 10m
                });
            });

            // 3. ConsumeAsync debe ser denegado (403 Forbidden)
            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _pantryAppService.ConsumeAsync(itemA.Id);
            });

            // 4. DeleteAsync debe ser denegado (403 Forbidden)
            await Should.ThrowAsync<AbpAuthorizationException>(async () =>
            {
                await _pantryAppService.DeleteAsync(itemA.Id);
            });
        }
    }
}
