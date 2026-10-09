using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace SmartPantry.Pantry;

public class PantryItemWarning : AuditedAggregateRoot<Guid>
{
    public Guid PantryItemId { get; private set; }
    public string WarningType { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;

    // Constructor sin parámetros requerido por Entity Framework Core
    protected PantryItemWarning()
    {
    }

    public PantryItemWarning(
        Guid id,
        Guid pantryItemId,
        string warningType,
        string message,
        bool isActive = true) : base(id)
    {
        PantryItemId = Check.NotNull(pantryItemId, nameof(pantryItemId));
        if (pantryItemId == Guid.Empty)
        {
            throw new BusinessException("SmartPantry:PantryItemWarning:InvalidPantryItemId", "El PantryItemId no puede ser vacío.");
        }

        WarningType = Check.NotNullOrWhiteSpace(warningType, nameof(warningType), PantryWarningConsts.MaxWarningTypeLength);
        Message = Check.NotNullOrWhiteSpace(message, nameof(message), PantryWarningConsts.MaxMessageLength);
        IsActive = isActive;
    }

    public void UpdateMessage(string message)
    {
        Message = Check.NotNullOrWhiteSpace(message, nameof(message), PantryWarningConsts.MaxMessageLength);
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate(string message)
    {
        UpdateMessage(message);
        IsActive = true;
    }
}

