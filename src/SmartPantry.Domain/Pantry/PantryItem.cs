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
    public decimal? Quantity { get; private set; }
    public string? Unit { get; private set; }

    // Constructor sin parámetros requerido por Entity Framework Core
    protected PantryItem()
    {
    }

    public PantryItem(
        Guid id,
        Guid userId,
        Guid productId,
        DateTime? expirationDate = null,
        PantryItemState state = PantryItemState.Active,
        decimal? quantity = null,
        string? unit = null) : base(id)
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
        SetQuantity(quantity);
        SetUnit(unit);
    }

    public void SetExpirationDate(DateTime? expirationDate)
    {
        ExpirationDate = expirationDate;
    }

    public void SetQuantity(decimal? quantity)
    {
        if (quantity.HasValue && quantity.Value < 0)
        {
            throw new BusinessException("SmartPantry:PantryItem:NegativeQuantity", "La cantidad no puede ser negativa.");
        }
        Quantity = quantity;
    }

    public void SetUnit(string? unit)
    {
        var normalized = unit?.Trim();
        if (!string.IsNullOrEmpty(normalized) && normalized.Length > PantryConsts.MaxUnitLength)
        {
            throw new BusinessException("SmartPantry:PantryItem:UnitTooLong", $"La unidad no puede superar los {PantryConsts.MaxUnitLength} caracteres.");
        }
        Unit = string.IsNullOrEmpty(normalized) ? null : normalized;
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
