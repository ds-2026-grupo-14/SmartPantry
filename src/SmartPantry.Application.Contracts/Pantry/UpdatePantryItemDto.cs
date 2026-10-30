using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartPantry.Pantry;

public class UpdatePantryItemDto : IValidatableObject
{
    [Range(0, double.MaxValue)]
    public decimal? Quantity { get; set; }

    [StringLength(PantryConsts.MaxUnitLength)]
    public string? Unit { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpirationDate.HasValue && (ExpirationDate.Value.Year < 1900 || ExpirationDate.Value.Year > 2100))
        {
            yield return new ValidationResult("La fecha de vencimiento debe ser una fecha válida del calendario.", new[] { nameof(ExpirationDate) });
        }
    }
}
