using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Zeiss.Products.Api.Errors;
using Zeiss.Products.Api.Responses;
using Zeiss.Products.Application.Products.Requests.ChangeStock;
using Zeiss.Products.Application.Products.Requests.CreateProduct;
using Zeiss.Products.Application.Products.Requests.SearchProducts;
using Zeiss.Products.Application.Products.Requests.StockLevel;
using Zeiss.Products.Application.Products.Requests.UpdateProduct;
using Zeiss.Products.Application.Products.Services;
using Zeiss.Products.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ErrorResponse
    {
        Message = "The request contains invalid values.",
        Errors = context.ModelState
            .Where(field => field.Value!.Errors.Count > 0)
            .ToDictionary(field => field.Key, field => field.Value!.Errors
                .Select(error => string.IsNullOrEmpty(error.ErrorMessage)
                    ? "The supplied value is invalid."
                    : error.ErrorMessage)
                .ToArray())
    });
});
builder.Services.AddSwaggerGen(options =>
{
    options.DescribeAllParametersInCamelCase();
});
builder.Services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateProductRequest>, UpdateProductRequestValidator>();
builder.Services.AddScoped<IValidator<ChangeStockRequest>, ChangeStockRequestValidator>();
builder.Services.AddScoped<IValidator<SearchProductsRequest>, SearchProductsRequestValidator>();
builder.Services.AddScoped<IValidator<StockLevelRequest>, StockLevelRequestValidator>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var connectionString = builder.Configuration.GetConnectionString("Products")
    ?? throw new InvalidOperationException("Connection string 'Products' is required.");
builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

// DomainExceptionHandler handles business errors first. Anything else is logged by the
// middleware and returns a generic message without technical details.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ErrorResponse
    {
        Message = "An unexpected error occurred while processing the request."
    });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes the entry point to the HTTP integration tests.
public partial class Program { }
