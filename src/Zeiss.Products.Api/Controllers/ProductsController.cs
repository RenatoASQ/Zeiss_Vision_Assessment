using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Zeiss.Products.Api.Responses;
using Zeiss.Products.Application.Products.Requests.ChangeStock;
using Zeiss.Products.Application.Products.Requests.CreateProduct;
using Zeiss.Products.Application.Products.Requests.SearchProducts;
using Zeiss.Products.Application.Products.Requests.StockLevel;
using Zeiss.Products.Application.Products.Requests.UpdateProduct;
using Zeiss.Products.Application.Products.Responses;
using Zeiss.Products.Application.Products.Services;

namespace Zeiss.Products.Api.Controllers;

// Each action validates its request explicitly before calling the service.
// DomainException is translated to 409 by DomainExceptionHandler.
[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpPost]
    [EndpointSummary("Create a product")]
    [EndpointDescription("Creates a product with a unique six-digit ID.")]
    public async Task<ActionResult<ProductResponse>> CreateProduct(
        CreateProductRequest request,
        [FromServices] IValidator<CreateProductRequest> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "The request contains invalid values.",
                Errors = validationResult.ToDictionary()
            });
        }

        var product = await _productService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, product);
    }

    [HttpGet("{id:int}")]
    [EndpointSummary("Get a product by ID")]
    [EndpointDescription("Returns the product's name, description, current stock and creation date.")]
    public async Task<ActionResult<ProductResponse>> GetProductById(int id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetByIdAsync(id, cancellationToken);
        return product is null ? ProductNotFound() : Ok(product);
    }

    [HttpGet]
    [EndpointSummary("List all products")]
    [EndpointDescription("Returns all non-deleted products, including their current stock, ordered by ID.")]
    public async Task<ActionResult<List<ProductResponse>>> GetProducts(CancellationToken cancellationToken)
    {
        return Ok(await _productService.GetAllAsync(cancellationToken));
    }

    [HttpPut("{id:int}")]
    [EndpointSummary("Update a product")]
    [EndpointDescription("Replaces the name and description and sets the stock to the supplied balance. Omitting the description clears it.")]
    public async Task<ActionResult<ProductResponse>> UpdateProduct(
        int id, UpdateProductRequest request,
        [FromServices] IValidator<UpdateProductRequest> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "The request contains invalid values.",
                Errors = validationResult.ToDictionary()
            });
        }

        var product = await _productService.UpdateAsync(id, request, cancellationToken);
        return product is null ? ProductNotFound() : Ok(product);
    }

    [HttpDelete("{id:int}")]
    [EndpointSummary("Delete a product")]
    [EndpointDescription("Removes the product from normal queries and operations while preserving its last stock balance and movement history.")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken cancellationToken)
    {
        var deleted = await _productService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : ProductNotFound();
    }

    [HttpPost("{id:int}/add-to-stock/{quantity}")]
    [EndpointSummary("Add stock to a product")]
    [EndpointDescription("Increases the current stock by the positive quantity supplied in the URL and records an Increment movement.")]
    public async Task<ActionResult<ProductResponse>> AddToStock(
        int id, [FromRoute] ChangeStockRequest request,
        [FromServices] IValidator<ChangeStockRequest> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "The request contains invalid values.",
                Errors = validationResult.ToDictionary()
            });
        }

        var product = await _productService.AddToStockAsync(id, request.Quantity, cancellationToken);
        return product is null ? ProductNotFound() : Ok(product);
    }

    [HttpPost("{id:int}/decrement-stock/{quantity}")]
    [EndpointSummary("Remove stock from a product")]
    [EndpointDescription("Reduces the current stock by the positive quantity supplied in the URL and records a Decrement movement")]
    public async Task<ActionResult<ProductResponse>> DecrementStock(
        int id, [FromRoute] ChangeStockRequest request,
        [FromServices] IValidator<ChangeStockRequest> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "The request contains invalid values.",
                Errors = validationResult.ToDictionary()
            });
        }

        var product = await _productService.DecrementStockAsync(id, request.Quantity, cancellationToken);
        return product is null ? ProductNotFound() : Ok(product);
    }

    [HttpGet("search")]
    [EndpointSummary("Search products by name")]
    [EndpointDescription("Finds products whose names contain the supplied text, ignoring case and accents.")]
    public async Task<ActionResult<List<ProductResponse>>> SearchProducts(
        [FromQuery] SearchProductsRequest request,
        [FromServices] IValidator<SearchProductsRequest> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "The request contains invalid values.",
                Errors = validationResult.ToDictionary()
            });
        }

        return Ok(await _productService.SearchAsync(request.Name, cancellationToken));
    }

    [HttpGet("stock-level")]
    [EndpointSummary("Find products within a stock range")]
    [EndpointDescription("Returns non-deleted products whose current stock is between min and max, including both limits.")]
    public async Task<ActionResult<List<ProductResponse>>> GetProductsByStockLevel(
        [FromQuery] StockLevelRequest request,
        [FromServices] IValidator<StockLevelRequest> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Message = "The request contains invalid values.",
                Errors = validationResult.ToDictionary()
            });
        }

        // The validator guarantees both bounds are present.
        return Ok(await _productService.GetByStockLevelAsync(request.Min!.Value, request.Max!.Value, cancellationToken));
    }

    private NotFoundObjectResult ProductNotFound() => NotFound(new ErrorResponse { Message = "Product not found." });
}
