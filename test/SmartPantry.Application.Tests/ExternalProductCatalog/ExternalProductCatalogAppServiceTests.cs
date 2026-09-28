using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace SmartPantry.ExternalProductCatalog;

public class ExternalProductCatalogAppServiceTests
{
    private readonly IExternalProductCatalogAppService _appService;
    private readonly IExternalProductCatalogClient _mockExternalClient;

    public ExternalProductCatalogAppServiceTests()
    {
        _mockExternalClient = Substitute.For<IExternalProductCatalogClient>();
        _appService = new ExternalProductCatalogAppService(_mockExternalClient);
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
}

