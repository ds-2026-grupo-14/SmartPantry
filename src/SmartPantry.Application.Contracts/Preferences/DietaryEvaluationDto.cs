using System.Collections.Generic;

namespace SmartPantry.Preferences;

public class DietaryEvaluationDto
{
    public DietaryVerificationStatus VerificationStatus { get; set; } = DietaryVerificationStatus.NotConfigured;

    public List<string> MatchedTerms { get; set; } = new();

    public string? InformationalMessage { get; set; }
}
