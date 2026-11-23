using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SmartPantry.Preferences;
using Xunit;

namespace SmartPantry.EntityFrameworkCore.Applications;

[Collection(SmartPantryTestConsts.CollectionDefinitionName)]
public class EfCoreDietaryPreferenceAppServiceTests
    : DietaryPreferenceAppServiceTests<SmartPantryEntityFrameworkCoreTestModule>
{
    [Fact]
    public async Task Should_Enforce_Unique_UserId_Index_In_AppUserDietaryPreferences()
    {
        var user = await CreateTestUserAsync("unique_idx");

        await WithUnitOfWorkAsync(async () =>
        {
            await PreferenceRepository.InsertAsync(
                new UserDietaryPreference(Guid.NewGuid(), user.Id, new[] { "gluten" }, true),
                autoSave: true);
        });

        await Should.ThrowAsync<DbUpdateException>(async () =>
        {
            await WithUnitOfWorkAsync(async () =>
            {
                await PreferenceRepository.InsertAsync(
                    new UserDietaryPreference(Guid.NewGuid(), user.Id, new[] { "mani" }, true),
                    autoSave: true);
            });
        });
    }
}
