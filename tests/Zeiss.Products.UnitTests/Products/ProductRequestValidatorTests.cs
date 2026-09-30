using Zeiss.Products.Application.Products.Requests.ChangeStock;
using Zeiss.Products.Application.Products.Requests.SearchProducts;
using Zeiss.Products.Application.Products.Requests.StockLevel;
using Zeiss.Products.Application.Products.Requests.UpdateProduct;

namespace Zeiss.Products.UnitTests.Products;

public class ProductRequestValidatorTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(int.MaxValue, true)]
    public void ChangeStock_RequiresPositiveQuantity(int quantity, bool expected)
    {
        var result = new ChangeStockRequestValidator().Validate(new ChangeStockRequest { Quantity = quantity });
        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateAndSearch_RejectMissingOrBlankName(string? name)
    {
        Assert.False(new UpdateProductRequestValidator().Validate(new UpdateProductRequest { Name = name!, Stock = 0 }).IsValid);
        Assert.False(new SearchProductsRequestValidator().Validate(new SearchProductsRequest { Name = name! }).IsValid);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void UpdateAndSearch_ApplyTheLengthLimitAfterTrimming(int length, bool expected)
    {
        var name = $"  {new string('a', length)}  ";
        Assert.Equal(expected, new UpdateProductRequestValidator().Validate(new UpdateProductRequest { Name = name, Stock = 0 }).IsValid);
        Assert.Equal(expected, new SearchProductsRequestValidator().Validate(new SearchProductsRequest { Name = name }).IsValid);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(int.MaxValue, true)]
    public void Update_RequiresAnExplicitNonNegativeStock(int? stock, bool expected)
    {
        var result = new UpdateProductRequestValidator().Validate(new UpdateProductRequest { Name = "Lenses", Stock = stock });
        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(null, 10, false)]
    [InlineData(0, null, false)]
    [InlineData(-1, 10, false)]
    [InlineData(0, -1, false)]
    [InlineData(10, 5, false)]
    [InlineData(0, 0, true)]
    [InlineData(10, 10, true)]
    [InlineData(0, int.MaxValue, true)]
    public void StockLevel_RequiresBothBoundsAndAnOrderedNonNegativeRange(int? min, int? max, bool expected)
    {
        var result = new StockLevelRequestValidator().Validate(new StockLevelRequest { Min = min, Max = max });
        Assert.Equal(expected, result.IsValid);
    }
}
