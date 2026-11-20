using System.Collections.Generic;
using SmartPantry.Preferences;

namespace SmartPantry.ExternalProductCatalog;

public class DietaryEvaluationDto
{
    public DietaryVerificationStatus VerificationStatus { get; set; } = DietaryVerificationStatus.NotConfigured;
    public List<string> MatchedTerms { get; set; } = [];
    public string? InformationalMessage { get; set; }
}
