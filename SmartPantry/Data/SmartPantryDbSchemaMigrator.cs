using Volo.Abp.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace SmartPantry.Data;

public class SmartPantryDbSchemaMigrator : ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public SmartPantryDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        
        /* We intentionally resolving the SmartPantryDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<SmartPantryDbContext>()
            .Database
            .MigrateAsync();

    }
}
