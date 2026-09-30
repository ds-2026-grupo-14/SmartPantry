namespace SmartPantry.OpenFoodFacts;

public class OpenFoodFactsOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
}
