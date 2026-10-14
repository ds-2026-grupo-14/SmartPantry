using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace SmartPantry.Pantry;

public class PantryItem : AuditedAggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid ProductId { get; private set; }
    public PantryItemState State { get; private set; }
    public DateTime? ExpirationDate { get; private set; }

    // Constructor sin parámetros requerido por Entity Framework Core
    protected PantryItem()
    {
    }

    public PantryItem(
        Guid id,
        Guid userId,
        Guid productId,
        DateTime? expirationDate = null,
        PantryItemState state = PantryItemState.Active) : base(id)
    {
        UserId = Check.NotNull(userId, nameof(userId));
        if (userId == Guid.Empty)
        {
            throw new BusinessException("SmartPantry:PantryItem:InvalidUserId", "El UserId no puede ser vacío.");
        }

        ProductId = Check.NotNull(productId, nameof(productId));
        if (productId == Guid.Empty)
        {
            throw new BusinessException("SmartPantry:PantryItem:InvalidProductId", "El ProductId no puede ser vacío.");
        }

        ExpirationDate = expirationDate;
        State = state;
    }

    public void SetExpirationDate(DateTime? expirationDate)
    {
        ExpirationDate = expirationDate;
    }

    public void Consume()
    {
        State = PantryItemState.Consumed;
    }

    public void Discard()
    {
        State = PantryItemState.Discarded;
    }

    public void SetActive()
    {
        State = PantryItemState.Active;
    }
}

