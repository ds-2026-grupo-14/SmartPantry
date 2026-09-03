using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartPantry.Data;

public class SmartPantryDbContextFactory : IDesignTimeDbContextFactory<SmartPantryDbContext>
{
    public SmartPantryDbContext CreateDbContext(string[] args)
    {
        SmartPantryGlobalFeatureConfigurator.Configure();
        SmartPantryModuleExtensionConfigurator.Configure();

        SmartPantryEfCoreEntityExtensionMappings.Configure();
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<SmartPantryDbContext>()
            .UseSqlServer(configuration.GetConnectionString("Default"));

        return new SmartPantryDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables();

        return builder.Build();
    }
}