using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using SmartPantry.ExternalProductCatalog;
using SmartPantry.Preferences;
using SmartPantry.Products;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Validation;
using Xunit;

namespace SmartPantry.EntityFrameworkCore.Applications;

[Collection(SmartPantryTestConsts.CollectionDefinitionName)]
public class EfCoreExternalProductCatalogAppServiceTests : SmartPantryEntityFrameworkCoreTestBase
{
    private readonly IExternalProductCatalogClient _externalClient;
    private readonly IExternalProductCatalogAppService _catalogAppService;
    private readonly IProductAppService _productAppService;

    public EfCoreExternalProductCatalogAppServiceTests()
    {
        _externalClient = GetRequiredService<IExternalProductCatalogClient>();
        _catalogAppService = GetRequiredService<IExternalProductCatalogAppService>();
        _productAppService = GetRequiredService<IProductAppService>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        base.AfterAddApplication(services);
        services.AddSingleton(Substitute.For<IExternalProductCatalogClient>());
    }

    [Fact]
    public async Task Should_Batch_Enrich_InternalProductIds_From_Sqlite_On_SearchAsync()
    {
        // Arrange
        const string savedBarcode = "7791337601015";
        const string unsavedBarcode = "7790895000997";

        var savedProduct = await _productAppService.SaveFromExternalAsync(new SaveExternalProductDto
        {
            Barcode = savedBarcode,
            Name = "Natural Yogurt",
            Category = ItemCategory.Dairy,
            Brand = "Test Dairy"
        });

        _externalClient
            .SearchByTextAsync("yogurt", 0, ExternalCatalogConsts.DefaultSearchMaxResultCount)
            .Returns(new PagedResultDto<ExternalProductDto>(
                2,
                [
                    new ExternalProductDto { Barcode = savedBarcode, Name = "Natural Yogurt" },
                    new ExternalProductDto { Barcode = unsavedBarcode, Name = "Chocolate Cookies" }
                ]));

        // Act
        var result = await _catalogAppService.SearchAsync(new SearchExternalProductsDto
        {
            Query = "yogurt",
            SkipCount = 0,
            MaxResultCount = ExternalCatalogConsts.DefaultSearchMaxResultCount
        });

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.Found);
        result.Results.ShouldNotBeNull();
        result.Results.TotalCount.ShouldBe(2);
        result.Results.Items.Count.ShouldBe(2);

        var savedItem = result.Results.Items.Single(x => x.Barcode == savedBarcode);
        savedItem.InternalProductId.ShouldBe(savedProduct.Id);
        savedItem.DietaryEvaluation.ShouldNotBeNull();
        savedItem.DietaryEvaluation.VerificationStatus.ShouldBe(DietaryVerificationStatus.NotConfigured);

        var unsavedItem = result.Results.Items.Single(x => x.Barcode == unsavedBarcode);
        unsavedItem.InternalProductId.ShouldBeNull();
        unsavedItem.DietaryEvaluation.ShouldNotBeNull();
        unsavedItem.DietaryEvaluation.VerificationStatus.ShouldBe(DietaryVerificationStatus.NotConfigured);
    }

    [Fact]
    public async Task Should_Resolve_InternalProductId_From_Sqlite_On_GetByBarcodeAsync()
    {
        // Arrange
        const string savedBarcode = "7790895000997";

        var savedProduct = await _productAppService.SaveFromExternalAsync(new SaveExternalProductDto
        {
            Barcode = savedBarcode,
            Name = "Chocolate Cookies",
            Category = ItemCategory.Snacks,
            Brand = "Bagley"
        });

        _externalClient
            .GetByBarcodeAsync(savedBarcode)
            .Returns(new ExternalProductDto
            {
                Barcode = savedBarcode,
                Name = "Chocolate Cookies"
            });

        // Act
        var result = await _catalogAppService.GetByBarcodeAsync(new GetExternalProductByBarcodeDto
        {
            Barcode = savedBarcode
        });

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.Found);
        result.Product.ShouldNotBeNull();
        result.Product.Barcode.ShouldBe(savedBarcode);
        result.Product.InternalProductId.ShouldBe(savedProduct.Id);
        result.Product.DietaryEvaluation.ShouldNotBeNull();
        result.Product.DietaryEvaluation.VerificationStatus.ShouldBe(DietaryVerificationStatus.NotConfigured);
    }

    public static TheoryData<string, int, int> InvalidSearchInputs() => new()
    {
        { string.Empty, 0, ExternalCatalogConsts.DefaultSearchMaxResultCount },
        { new string('a', ExternalCatalogConsts.MinSearchQueryLength - 1), 0, ExternalCatalogConsts.DefaultSearchMaxResultCount },
        { $" {new string('a', ExternalCatalogConsts.MinSearchQueryLength - 1)} ", 0, ExternalCatalogConsts.DefaultSearchMaxResultCount },
        { new string('a', ExternalCatalogConsts.MaxSearchQueryLength + 1), 0, ExternalCatalogConsts.DefaultSearchMaxResultCount },
        { "yogurt", -1, ExternalCatalogConsts.DefaultSearchMaxResultCount },
        { "yogurt", 0, ExternalCatalogConsts.MinSearchMaxResultCount - 1 },
        { "yogurt", 0, ExternalCatalogConsts.MaxSearchMaxResultCount + 1 }
    };

    [Theory]
    [MemberData(nameof(InvalidSearchInputs))]
    public async Task Should_Reject_Invalid_SearchExternalProductsDto_Through_Abp_Validation(
        string query,
        int skipCount,
        int maxResultCount)
    {
        // Arrange
        var input = new SearchExternalProductsDto
        {
            Query = query,
            SkipCount = skipCount,
            MaxResultCount = maxResultCount
        };

        // Act & Assert
        await Should.ThrowAsync<AbpValidationException>(async () =>
        {
            await _catalogAppService.SearchAsync(input);
        });

        await _externalClient.DidNotReceive().SearchByTextAsync(
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>());
    }
}
