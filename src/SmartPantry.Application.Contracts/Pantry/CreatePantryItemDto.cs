using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartPantry.Pantry;

public class CreatePantryItemDto : IValidatableObject
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Quantity { get; set; }

    [StringLength(PantryConsts.MaxUnitLength)]
    public string? Unit { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProductId == Guid.Empty)
        {
            yield return new ValidationResult("El ProductId es obligatorio.", new[] { nameof(ProductId) });
        }

        if (ExpirationDate.HasValue && (ExpirationDate.Value.Year < 1900 || ExpirationDate.Value.Year > 2100))
        {
            yield return new ValidationResult("La fecha de vencimiento debe ser una fecha válida del calendario.", new[] { nameof(ExpirationDate) });
        }
    }
}
