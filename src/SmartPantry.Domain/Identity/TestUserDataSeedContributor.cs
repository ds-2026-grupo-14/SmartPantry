using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace SmartPantry.Identity;

public class TestUserDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IdentityUserManager _userManager;
    private readonly IConfiguration _configuration;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public TestUserDataSeedContributor(
        IdentityUserManager userManager,
        IConfiguration configuration,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _userManager = userManager;
        _configuration = configuration;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        using (_currentTenant.Change(context?.TenantId))
        {
            const string userName = "user";
            if (await _userManager.FindByNameAsync(userName) != null)
            {
                return; // Idempotente
            }

            var password = _configuration["Seed:TestUser:Password"] ?? "1q2w3E*";
            var user = new IdentityUser(
                _guidGenerator.Create(),
                userName,
                "user@smartpantry.local",
                context?.TenantId
            );
            user.SetEmailConfirmed(true);

            (await _userManager.CreateAsync(user, password)).CheckErrors();
            // Deliberadamente NO se asigna el rol "admin"
        }
    }
}
