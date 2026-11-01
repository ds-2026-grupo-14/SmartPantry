using System;
using Volo.Abp.Application.Dtos;

namespace SmartPantry.Pantry;

public class PantryItemDto : AuditedEntityDto<Guid>
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public PantryItemState State { get; set; }
}
