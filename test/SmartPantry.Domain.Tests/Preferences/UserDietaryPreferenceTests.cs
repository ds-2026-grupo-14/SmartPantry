using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace SmartPantry.Preferences;

public class UserDietaryPreferenceTests
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void Should_Create_Preference_With_Defaults_When_Terms_Are_Null()
    {
        var preference = new UserDietaryPreference(Guid.NewGuid(), _userId);

        preference.UserId.ShouldBe(_userId);
        preference.WatchedTerms.ShouldBe(string.Empty);
        preference.GetWatchedTermsList().ShouldBeEmpty();
        preference.ShowAlertsOnScreen.ShouldBeTrue();
    }

    [Fact]
    public void Should_Normalize_Accents_Case_And_Whitespace()
    {
        var preference = new UserDietaryPreference(
            Guid.NewGuid(),
            _userId,
            new[] { "  Maní ", "LÁCTEOS", "Azúcar   Refinada", "  Gluten\t" },
            showAlertsOnScreen: true);

        preference.WatchedTerms.ShouldBe("mani;lacteos;azucar refinada;gluten");
        preference.GetWatchedTermsList().ShouldBe(new[] { "mani", "lacteos", "azucar refinada", "gluten" });
    }

    [Fact]
    public void Should_Deduplicate_Equivalent_Terms_Preserving_Order()
    {
        var preference = new UserDietaryPreference(
            Guid.NewGuid(),
            _userId,
            new[] { "Maní", "Gluten", "mani", "MANÍ", "  gluten  ", "Soja" });

        preference.GetWatchedTermsList().ShouldBe(new[] { "mani", "gluten", "soja" });
    }

    [Fact]
    public void Should_Ignore_Null_Or_Whitespace_Entries()
    {
        var preference = new UserDietaryPreference(
            Guid.NewGuid(),
            _userId,
            new[] { "", "   ", null!, "Lactosa", "\t" });

        preference.GetWatchedTermsList().ShouldBe(new[] { "lactosa" });
    }

    [Fact]
    public void Should_Reject_Empty_UserId()
    {
        var ex = Should.Throw<BusinessException>(() =>
        {
            _ = new UserDietaryPreference(Guid.NewGuid(), Guid.Empty, new[] { "gluten" });
        });

        ex.Code.ShouldBe("SmartPantry:UserDietaryPreference:InvalidUserId");
    }

    [Theory]
    [InlineData("a")]
    [InlineData(" á ")]
    [InlineData(";")]
    [InlineData(";a;")]
    public void Should_Reject_Term_Shorter_Than_MinLength(string shortTerm)
    {
        var ex = Should.Throw<BusinessException>(() =>
        {
            _ = new UserDietaryPreference(Guid.NewGuid(), _userId, new[] { shortTerm });
        });

        ex.Code.ShouldBe("SmartPantry:UserDietaryPreference:TermTooShort");
    }

    [Fact]
    public void Should_Reject_Term_Exceeding_MaxLength()
    {
        var tooLongTerm = new string('a', PreferenceConsts.MaxTermLength + 1);

        var ex = Should.Throw<BusinessException>(() =>
        {
            _ = new UserDietaryPreference(Guid.NewGuid(), _userId, new[] { tooLongTerm });
        });

        ex.Code.ShouldBe("SmartPantry:UserDietaryPreference:TermTooLong");
    }

    [Fact]
    public void Should_Accept_Terms_At_Exact_Length_Boundaries()
    {
        var minTerm = new string('a', PreferenceConsts.MinTermLength);
        var maxTerm = new string('b', PreferenceConsts.MaxTermLength);

        var preference = new UserDietaryPreference(
            Guid.NewGuid(),
            _userId,
            new[] { minTerm, maxTerm });

        preference.GetWatchedTermsList().ShouldBe(new[] { minTerm, maxTerm });
    }

    [Fact]
    public void Should_Accept_MaxWatchedTermsCount_Even_With_Extra_Duplicates()
    {
        var uniqueTerms = Enumerable.Range(1, PreferenceConsts.MaxWatchedTermsCount)
            .Select(i => $"termino{i:D2}")
            .ToList();
        uniqueTerms.Add("TERMINO01");
        uniqueTerms.Add("Término02");

        var preference = new UserDietaryPreference(Guid.NewGuid(), _userId, uniqueTerms);

        preference.GetWatchedTermsList().Count.ShouldBe(PreferenceConsts.MaxWatchedTermsCount);
    }

    [Fact]
    public void Should_Reject_More_Than_MaxWatchedTermsCount_Unique_Terms()
    {
        var tooManyTerms = Enumerable.Range(1, PreferenceConsts.MaxWatchedTermsCount + 1)
            .Select(i => $"termino{i:D2}");

        var ex = Should.Throw<BusinessException>(() =>
        {
            _ = new UserDietaryPreference(Guid.NewGuid(), _userId, tooManyTerms);
        });

        ex.Code.ShouldBe("SmartPantry:UserDietaryPreference:TooManyWatchedTerms");
    }

    [Fact]
    public void Should_Reject_Serialized_Terms_Exceeding_MaxSerializedLength()
    {
        // 20 términos únicos de 60 caracteres c/u -> ~1219 chars (> 1024), aun respetando <= 30 términos
        var longTerms = Enumerable.Range(1, 20)
            .Select(i => $"t{i:D2}" + new string('x', 57));

        var ex = Should.Throw<BusinessException>(() =>
        {
            _ = new UserDietaryPreference(Guid.NewGuid(), _userId, longTerms);
        });

        ex.Code.ShouldBe("SmartPantry:UserDietaryPreference:WatchedTermsOverflow");
    }

    [Fact]
    public void Should_Update_Terms_And_Toggle_ShowAlertsOnScreen()
    {
        var preference = new UserDietaryPreference(
            Guid.NewGuid(),
            _userId,
            new[] { "gluten" },
            showAlertsOnScreen: true);

        preference.SetWatchedTerms(new[] { "Maní", "Soja" });
        preference.SetShowAlertsOnScreen(false);

        preference.GetWatchedTermsList().ShouldBe(new[] { "mani", "soja" });
        preference.ShowAlertsOnScreen.ShouldBeFalse();

        preference.SetWatchedTerms(null);
        preference.WatchedTerms.ShouldBe(string.Empty);
        preference.GetWatchedTermsList().ShouldBeEmpty();
    }
}
