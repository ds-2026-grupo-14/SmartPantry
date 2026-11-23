using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartPantry.ExternalProductCatalog;

public class SearchExternalProductsDto : IValidatableObject
{
    [Required]
    [StringLength(ExternalCatalogConsts.MaxSearchQueryLength, MinimumLength = ExternalCatalogConsts.MinSearchQueryLength)]
    public string Query { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int SkipCount { get; set; } = 0;

    [Range(ExternalCatalogConsts.MinSearchMaxResultCount, ExternalCatalogConsts.MaxSearchMaxResultCount)]
    public int MaxResultCount { get; set; } = ExternalCatalogConsts.DefaultSearchMaxResultCount;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var trimmedLength = Query?.Trim().Length ?? 0;
        if (trimmedLength < ExternalCatalogConsts.MinSearchQueryLength ||
            trimmedLength > ExternalCatalogConsts.MaxSearchQueryLength)
        {
            yield return new ValidationResult(
                $"The field {nameof(Query)} must be a string with a trimmed length between {ExternalCatalogConsts.MinSearchQueryLength} and {ExternalCatalogConsts.MaxSearchQueryLength}.",
                [nameof(Query)]);
        }
    }
}
