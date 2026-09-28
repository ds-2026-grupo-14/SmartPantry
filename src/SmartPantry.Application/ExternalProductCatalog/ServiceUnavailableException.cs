using System;

namespace SmartPantry.ExternalProductCatalog;

public class ServiceUnavailableException : Exception
{
    public ServiceUnavailableException() : base("External catalog service is currently unavailable.") {}
}
