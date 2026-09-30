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
using Zeiss.Products.Domain.Exceptions;

namespace Zeiss.Products.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    [HttpPost]
    [EndpointSummary("Create a product")]
    [EndpointDescription("Creates a product with a unique six-digit ID.")]
    public async Task<ActionResult<ProductResponse>> CreateProduct(
        CreateProductRequest request,
        [FromServices] IProductService productService,
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

        try
        {
            var product = await productService.CreateAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, product);
        }
        catch (DomainException exception)
        {
            return Conflict(new ErrorResponse { Message = exception.Message });
        }
    }

    [HttpGet("{id:int}")]
    [EndpointSummary("Get a product by ID")]
    [EndpointDescription("Returns the product's name, current stock and creation date")]
    public async Task<ActionResult<ProductResponse>> GetProductById(
        int id,
        [FromServices] IProductService productService,
        CancellationToken cancellationToken)
    {
        var product = await productService.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return NotFound(new ErrorResponse { Message = "Product not found." });
        }

        return Ok(product);
    }

    [HttpGet]
    [EndpointSummary("List all products")]
    [EndpointDescription("Returns all non-deleted products, including their current stock, ordered by ID.")]
    public async Task<ActionResult<List<ProductResponse>>> GetProducts(
        [FromServices] IProductService productService,
        CancellationToken cancellationToken)
    {
        var products = await productService.GetAllAsync(cancellationToken);
        return Ok(products);
    }

    [HttpPut("{id:int}")]
    [EndpointSummary("Update product name and stock")]
    [EndpointDescription("Updates the name and sets the stock to the supplied balance.")]
    public async Task<ActionResult<ProductResponse>> UpdateProduct(
        int id, UpdateProductRequest request,
        [FromServices] IProductService productService,
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

        try
        {
            var product = await productService.UpdateAsync(id, request, cancellationToken);
            if (product is null)
            {
                return NotFound(new ErrorResponse { Message = "Product not found." });
            }

            return Ok(product);
        }
        catch (DomainException exception)
        {
            return Conflict(new ErrorResponse { Message = exception.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [EndpointSummary("Delete a product")]
    [EndpointDescription("Removes the product from normal queries and operations while preserving its last stock balance and movement history.")]
    public async Task<IActionResult> DeleteProduct(
        int id,
        [FromServices] IProductService productService,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await productService.DeleteAsync(id, cancellationToken);
            if (!deleted)
            {
                return NotFound(new ErrorResponse { Message = "Product not found." });
            }

            return NoContent();
        }
        catch (DomainException exception)
        {
            return Conflict(new ErrorResponse { Message = exception.Message });
        }
    }

    [HttpPost("{id:int}/add-to-stock/{quantity}")]
    [EndpointSummary("Add stock to a product")]
    [EndpointDescription("Increases the current stock by the positive quantity supplied in the URL and records an Increment movement.")]
    public async Task<ActionResult<ProductResponse>> AddToStock(
        int id, [FromRoute] ChangeStockRequest request,
        [FromServices] IProductService productService,
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

        try
        {
            var product = await productService.AddToStockAsync(id, request.Quantity, cancellationToken);
            if (product is null)
            {
                return NotFound(new ErrorResponse { Message = "Product not found." });
            }

            return Ok(product);
        }
        catch (DomainException exception)
        {
            return Conflict(new ErrorResponse { Message = exception.Message });
        }
    }

    [HttpPost("{id:int}/decrement-stock/{quantity}")]
    [EndpointSummary("Remove stock from a product")]
    [EndpointDescription("Reduces the current stock by the positive quantity supplied in the URL and records a Decrement movement")]
    public async Task<ActionResult<ProductResponse>> DecrementStock(
        int id, [FromRoute] ChangeStockRequest request,
        [FromServices] IProductService productService,
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

        try
        {
            var product = await productService.DecrementStockAsync(id, request.Quantity, cancellationToken);
            if (product is null)
            {
                return NotFound(new ErrorResponse { Message = "Product not found." });
            }

            return Ok(product);
        }
        catch (DomainException exception)
        {
            return Conflict(new ErrorResponse { Message = exception.Message });
        }
    }

    [HttpGet("search")]
    [EndpointSummary("Search products by name")]
    [EndpointDescription("Finds products whose names contain the supplied text, ignoring case and accents.")]
    public async Task<ActionResult<List<ProductResponse>>> SearchProducts(
        [FromQuery] SearchProductsRequest request,
        [FromServices] IProductService productService,
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

        var products = await productService.SearchAsync(request.Name, cancellationToken);
        return Ok(products);
    }

    [HttpGet("stock-level")]
    [EndpointSummary("Find products within a stock range")]
    [EndpointDescription("Returns non-deleted products whose current stock is between min and max, including both limits.")]
    public async Task<ActionResult<List<ProductResponse>>> GetProductsByStockLevel(
        [FromQuery] StockLevelRequest request,
        [FromServices] IProductService productService,
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

        var products = await productService.GetByStockLevelAsync(request.Min!.Value, request.Max!.Value, cancellationToken);
        return Ok(products);
    }
}
