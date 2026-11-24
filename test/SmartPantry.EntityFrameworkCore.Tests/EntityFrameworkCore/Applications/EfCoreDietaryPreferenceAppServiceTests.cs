using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SmartPantry.Preferences;
using Volo.Abp.Account;
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

    [Fact]
    public async Task Should_Read_And_Update_Current_User_Profile_Without_Affecting_Dietary_Preferences()
    {
        var profileAppService = GetRequiredService<IProfileAppService>();
        var user = await CreateTestUserAsync("profile_rf02");

        using (ChangeCurrentUser(user))
        {
            await DietaryPreferenceAppService.UpdateMyPreferencesAsync(new UpdateDietaryPreferenceDto
            {
                WatchedTerms = new List<string> { "gluten", "mani" },
                ShowAlertsOnScreen = true
            });

            var initialProfile = await profileAppService.GetAsync();
            initialProfile.UserName.ShouldBe(user.UserName);
            initialProfile.Email.ShouldBe(user.Email);

            var updatedEmail = $"{user.UserName}.updated@smartpantry.local";
            var updatedProfile = await profileAppService.UpdateAsync(new UpdateProfileDto
            {
                UserName = initialProfile.UserName,
                Email = updatedEmail,
                Name = "Nombre",
                Surname = "Usuario",
                ConcurrencyStamp = initialProfile.ConcurrencyStamp
            });

            updatedProfile.Name.ShouldBe("Nombre");
            updatedProfile.Surname.ShouldBe("Usuario");
            updatedProfile.Email.ShouldBe(updatedEmail);

            var reloadedProfile = await profileAppService.GetAsync();
            reloadedProfile.Name.ShouldBe("Nombre");
            reloadedProfile.Surname.ShouldBe("Usuario");
            reloadedProfile.Email.ShouldBe(updatedEmail);

            var preferences = await DietaryPreferenceAppService.GetMyPreferencesAsync();
            preferences.WatchedTerms.ShouldBe(new List<string> { "gluten", "mani" });
            preferences.ShowAlertsOnScreen.ShouldBeTrue();
        }
    }
}
