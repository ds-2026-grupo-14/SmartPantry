using System;

namespace SmartPantry.ExternalProductCatalog;

public class RateLimitExceededException : Exception
{
    public RateLimitExceededException() : base("Rate limit exceeded from external catalog.") {}
}
