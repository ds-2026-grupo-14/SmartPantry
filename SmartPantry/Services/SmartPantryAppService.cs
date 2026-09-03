using Volo.Abp.Application.Services;
using SmartPantry.Localization;

namespace SmartPantry.Services;

/* Inherit your application services from this class. */
public abstract class SmartPantryAppService : ApplicationService
{
    protected SmartPantryAppService()
    {
        LocalizationResource = typeof(SmartPantryResource);
    }
}