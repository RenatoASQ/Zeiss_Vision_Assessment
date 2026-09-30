using Zeiss.Products.Application.Products.Requests.CreateProduct;

namespace Zeiss.Products.UnitTests.Products;

public class CreateProductRequestValidatorTests
{
    private readonly CreateProductRequestValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Validate_WhenNameIsMissingOrWhitespace_ReturnsNameError(string? name)
    {
        var request = new CreateProductRequest { Name = name!, Stock = 0 };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(CreateProductRequest.Name), error.PropertyName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public void Validate_WhenTrimmedNameIsWithinLengthLimits_IsValid(int length)
    {
        var request = new CreateProductRequest
        {
            Name = $"  {new string('a', length)}  ",
            Stock = 0
        };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenTrimmedNameExceeds200Characters_ReturnsNameError()
    {
        var request = new CreateProductRequest
        {
            Name = $"  {new string('a', 201)}  ",
            Stock = 0
        };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProductRequest.Name));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void Validate_WhenStockIsNonNegative_IsValid(int stock)
    {
        var request = new CreateProductRequest { Name = "Contact lenses", Stock = stock };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_WhenStockIsNegative_ReturnsStockError(int stock)
    {
        var request = new CreateProductRequest { Name = "Contact lenses", Stock = stock };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(CreateProductRequest.Stock), error.PropertyName);
    }

    [Fact]
    public void Validate_WhenNameAndStockAreInvalid_ReturnsBothErrors()
    {
        var request = new CreateProductRequest { Name = " ", Stock = -1 };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProductRequest.Name));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateProductRequest.Stock));
    }
}
