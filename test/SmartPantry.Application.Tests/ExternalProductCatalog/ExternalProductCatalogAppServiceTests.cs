using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using SmartPantry.Preferences;
using SmartPantry.Products;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace SmartPantry.ExternalProductCatalog;

public class ExternalProductCatalogAppServiceTests
{
    private readonly IExternalProductCatalogAppService _appService;
    private readonly IExternalProductCatalogClient _mockExternalClient;
    private readonly IRepository<Product, Guid> _mockProductRepository;
    private readonly List<Product> _seededProducts = [];

    public ExternalProductCatalogAppServiceTests()
    {
        _mockExternalClient = Substitute.For<IExternalProductCatalogClient>();
        _mockProductRepository = Substitute.For<IRepository<Product, Guid>>();
        _mockProductRepository
            .GetListAsync(
                Arg.Any<Expression<Func<Product, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo => _seededProducts
                .Where(callInfo.ArgAt<Expression<Func<Product, bool>>>(0).Compile())
                .ToList());

        _appService = new ExternalProductCatalogAppService(_mockExternalClient, _mockProductRepository);
    }

    [Fact]
    public async Task Should_Return_Found_When_Product_Exists()
    {
        // Arrange
        var barcode = "123456789012";
        _mockExternalClient.GetByBarcodeAsync(barcode).Returns(new ExternalProductDto { Barcode = barcode });
        var input = new GetExternalProductByBarcodeDto { Barcode = barcode };

        // Act
        var result = await _appService.GetByBarcodeAsync(input);

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.Found);
        result.Product.ShouldNotBeNull();
        result.Product.Barcode.ShouldBe(barcode);
    }

    [Fact]
    public async Task Should_Return_Found_With_Missing_Fields_When_Product_Has_Partial_Data()
    {
        // Arrange
        var barcode = "123456789012";
        _mockExternalClient.GetByBarcodeAsync(barcode).Returns(new ExternalProductDto
        {
            Barcode = barcode,
            Name = "Agua Mineral",
            Brand = null,
            Ingredients = null
        });
        var input = new GetExternalProductByBarcodeDto { Barcode = barcode };

        // Act
        var result = await _appService.GetByBarcodeAsync(input);

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.Found);
        result.Product.ShouldNotBeNull();
        result.Product.Barcode.ShouldBe(barcode);
        result.Product.Name.ShouldBe("Agua Mineral");
        result.Product.Brand.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Product_Does_Not_Exist()
    {
        // Arrange
        var barcode = "123456789012";
        _mockExternalClient.GetByBarcodeAsync(barcode).Returns((ExternalProductDto?)null);
        var input = new GetExternalProductByBarcodeDto { Barcode = barcode };

        // Act
        var result = await _appService.GetByBarcodeAsync(input);

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.NotFound);
        result.Product.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Return_RateLimitExceeded_When_Client_Throws_Exception()
    {
        // Arrange
        var barcode = "123456789012";
        _mockExternalClient.GetByBarcodeAsync(barcode).Throws(new RateLimitExceededException());
        var input = new GetExternalProductByBarcodeDto { Barcode = barcode };

        // Act
        var result = await _appService.GetByBarcodeAsync(input);

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.RateLimitExceeded);
        result.Product.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Return_ServiceUnavailable_When_Client_Throws_Exception()
    {
        // Arrange
        var barcode = "123456789012";
        _mockExternalClient.GetByBarcodeAsync(barcode).Throws(new ServiceUnavailableException());
        var input = new GetExternalProductByBarcodeDto { Barcode = barcode };

        // Act
        var result = await _appService.GetByBarcodeAsync(input);

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.ServiceUnavailable);
        result.Product.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Resolve_InternalProductId_And_Set_NotConfigured_DietaryEvaluation_On_GetByBarcodeAsync_When_Saved()
    {
        // Arrange
        const string barcode = "7790895000997";
        var savedProduct = new Product(
            Guid.NewGuid(),
            barcode,
            "Chocolate Cookies",
            ItemCategory.Snacks);
        _seededProducts.Add(savedProduct);

        _mockExternalClient.GetByBarcodeAsync(barcode).Returns(new ExternalProductDto
        {
            Barcode = barcode,
            Name = "Chocolate Cookies"
        });

        // Act
        var result = await _appService.GetByBarcodeAsync(new GetExternalProductByBarcodeDto { Barcode = barcode });

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.Found);
        result.Product.ShouldNotBeNull();
        result.Product.InternalProductId.ShouldBe(savedProduct.Id);
        result.Product.DietaryEvaluation.ShouldNotBeNull();
        result.Product.DietaryEvaluation.VerificationStatus.ShouldBe(DietaryVerificationStatus.NotConfigured);
        result.Product.DietaryEvaluation.MatchedTerms.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Return_Null_InternalProductId_And_Set_NotConfigured_DietaryEvaluation_On_GetByBarcodeAsync_When_Unsaved()
    {
        // Arrange
        _seededProducts.Add(new Product(
            Guid.NewGuid(),
            "7790000000001",
            "Other Product",
            ItemCategory.Other));

        const string unsavedBarcode = "7790895000998";
        _mockExternalClient.GetByBarcodeAsync(unsavedBarcode).Returns(new ExternalProductDto
        {
            Barcode = unsavedBarcode,
            Name = "Unsaved Soda"
        });

        // Act
        var result = await _appService.GetByBarcodeAsync(new GetExternalProductByBarcodeDto { Barcode = unsavedBarcode });

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.Found);
        result.Product.ShouldNotBeNull();
        result.Product.InternalProductId.ShouldBeNull();
        result.Product.DietaryEvaluation.ShouldNotBeNull();
        result.Product.DietaryEvaluation.VerificationStatus.ShouldBe(DietaryVerificationStatus.NotConfigured);
        result.Product.DietaryEvaluation.MatchedTerms.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_Return_Paginated_Results_With_Resolved_InternalProductIds_On_SearchAsync()
    {
        // Arrange
        const string savedBarcode = "7791337601015";
        const string unsavedBarcode = "7790895000997";

        var savedProduct = new Product(
            Guid.NewGuid(),
            savedBarcode,
            "Natural Yogurt",
            ItemCategory.Dairy);
        _seededProducts.Add(savedProduct);
        _seededProducts.Add(new Product(
            Guid.NewGuid(),
            "7799999999999",
            "Unrelated Product",
            ItemCategory.Other));

        var externalItems = new List<ExternalProductDto>
        {
            new() { Barcode = savedBarcode, Name = "Natural Yogurt" },
            new() { Barcode = unsavedBarcode, Name = "Chocolate Cookies" }
        };

        _mockExternalClient
            .SearchByTextAsync("yogurt", 10, 20)
            .Returns(new PagedResultDto<ExternalProductDto>(28, externalItems));

        var input = new SearchExternalProductsDto
        {
            Query = "  yogurt  ",
            SkipCount = 10,
            MaxResultCount = 20
        };

        // Act
        var result = await _appService.SearchAsync(input);

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.Found);
        result.Results.ShouldNotBeNull();
        result.Results.TotalCount.ShouldBe(28);
        result.Results.Items.Count.ShouldBe(2);

        result.Results.Items[0].Barcode.ShouldBe(savedBarcode);
        result.Results.Items[0].InternalProductId.ShouldBe(savedProduct.Id);
        result.Results.Items[0].DietaryEvaluation.ShouldNotBeNull();
        result.Results.Items[0].DietaryEvaluation.VerificationStatus.ShouldBe(DietaryVerificationStatus.NotConfigured);
        result.Results.Items[0].DietaryEvaluation.MatchedTerms.ShouldBeEmpty();

        result.Results.Items[1].Barcode.ShouldBe(unsavedBarcode);
        result.Results.Items[1].InternalProductId.ShouldBeNull();
        result.Results.Items[1].DietaryEvaluation.ShouldNotBeNull();
        result.Results.Items[1].DietaryEvaluation.VerificationStatus.ShouldBe(DietaryVerificationStatus.NotConfigured);
        result.Results.Items[1].DietaryEvaluation.MatchedTerms.ShouldBeEmpty();

        await _mockExternalClient.Received(1).SearchByTextAsync("yogurt", 10, 20);
        await _mockProductRepository.Received(1).GetListAsync(
            Arg.Any<Expression<Func<Product, bool>>>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_Empty_Results_Without_Querying_Product_Repository_On_SearchAsync()
    {
        // Arrange
        _mockExternalClient
            .SearchByTextAsync("nonexistent", 0, ExternalCatalogConsts.DefaultSearchMaxResultCount)
            .Returns(new PagedResultDto<ExternalProductDto>(0, []));

        var input = new SearchExternalProductsDto
        {
            Query = "nonexistent",
            SkipCount = 0,
            MaxResultCount = ExternalCatalogConsts.DefaultSearchMaxResultCount
        };

        // Act
        var result = await _appService.SearchAsync(input);

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.Found);
        result.Results.ShouldNotBeNull();
        result.Results.TotalCount.ShouldBe(0);
        result.Results.Items.ShouldBeEmpty();

        await _mockProductRepository.DidNotReceive().GetListAsync(
            Arg.Any<Expression<Func<Product, bool>>>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_RateLimitExceeded_With_Empty_Results_On_SearchAsync()
    {
        // Arrange
        _mockExternalClient
            .SearchByTextAsync("yogurt", 0, ExternalCatalogConsts.DefaultSearchMaxResultCount)
            .Throws(new RateLimitExceededException());

        // Act
        var result = await _appService.SearchAsync(new SearchExternalProductsDto { Query = "yogurt" });

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.RateLimitExceeded);
        result.Results.ShouldNotBeNull();
        result.Results.TotalCount.ShouldBe(0);
        result.Results.Items.ShouldBeEmpty();

        await _mockProductRepository.DidNotReceive().GetListAsync(
            Arg.Any<Expression<Func<Product, bool>>>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_ServiceUnavailable_With_Empty_Results_On_SearchAsync()
    {
        // Arrange
        _mockExternalClient
            .SearchByTextAsync("yogurt", 0, ExternalCatalogConsts.DefaultSearchMaxResultCount)
            .Throws(new ServiceUnavailableException());

        // Act
        var result = await _appService.SearchAsync(new SearchExternalProductsDto { Query = "yogurt" });

        // Assert
        result.Status.ShouldBe(ExternalProductQueryStatus.ServiceUnavailable);
        result.Results.ShouldNotBeNull();
        result.Results.TotalCount.ShouldBe(0);
        result.Results.Items.ShouldBeEmpty();

        await _mockProductRepository.DidNotReceive().GetListAsync(
            Arg.Any<Expression<Func<Product, bool>>>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }
}

