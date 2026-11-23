using System;
using System.Collections.Generic;

namespace SmartPantry.Preferences;

public class DietaryPreferenceDto
{
    public Guid UserId { get; set; }

    public List<string> WatchedTerms { get; set; } = new();

    public bool ShowAlertsOnScreen { get; set; } = true;

    public DateTime? LastModificationTime { get; set; }
}
