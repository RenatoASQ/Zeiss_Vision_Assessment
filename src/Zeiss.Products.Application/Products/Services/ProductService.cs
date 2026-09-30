using Zeiss.Products.Application.Products.Interfaces;
using Zeiss.Products.Application.Products.Requests.CreateProduct;
using Zeiss.Products.Application.Products.Requests.UpdateProduct;
using Zeiss.Products.Application.Products.Responses;
using Zeiss.Products.Domain.Entities;
using Zeiss.Products.Domain.Enums;
using Zeiss.Products.Domain.Exceptions;

namespace Zeiss.Products.Application.Products.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await _productRepository.ExistsByNameAsync(name, null, cancellationToken))
        {
            throw new DomainException("A product with this name already exists.");
        }

        var product = new Product
        {
            Name = name,
            Stock = request.Stock,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (product.Stock > 0)
        {
            product.StockMovements.Add(new StockMovement
            {
                Type = StockMovementType.Increment,
                Quantity = product.Stock,
                StockBefore = 0,
                StockAfter = product.Stock,
                OccurredAtUtc = product.CreatedAtUtc
            });
        }

        await _productRepository.AddAsync(product, cancellationToken);
        return ToResponse(product);
    }

    public async Task<ProductResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        return product is null ? null : ToResponse(product);
    }

    public async Task<List<ProductResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);
        return products.Select(product => ToResponse(product)).ToList();
    }

    public async Task<List<ProductResponse>> SearchAsync(string name, CancellationToken cancellationToken)
    {
        var products = await _productRepository.SearchAsync(name.Trim(), cancellationToken);
        return products.Select(product => ToResponse(product)).ToList();
    }

    public async Task<List<ProductResponse>> GetByStockLevelAsync(int min, int max, CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetByStockLevelAsync(min, max, cancellationToken);
        return products.Select(product => ToResponse(product)).ToList();
    }

    public async Task<ProductResponse?> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var name = request.Name.Trim();
        if (await _productRepository.ExistsByNameAsync(name, id, cancellationToken))
        {
            throw new DomainException("A product with this name already exists.");
        }

        product.Name = name;
        var stockBefore = product.Stock;
        product.Stock = request.Stock!.Value;

        if (product.Stock != stockBefore)
        {
            product.StockMovements.Add(new StockMovement
            {
                Type = StockMovementType.Adjustment,
                Quantity = Math.Abs(product.Stock - stockBefore),
                StockBefore = stockBefore,
                StockAfter = product.Stock,
                OccurredAtUtc = DateTime.UtcNow
            });
        }

        await _productRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(product);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return false;
        }

        // Removing a catalog entry does not represent a physical stock movement.
        product.DeletedAtUtc = DateTime.UtcNow;
        await _productRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<ProductResponse?> AddToStockAsync(int id, int quantity, CancellationToken cancellationToken)
        => ChangeStockAsync(id, quantity, StockMovementType.Increment, cancellationToken);

    public Task<ProductResponse?> DecrementStockAsync(int id, int quantity, CancellationToken cancellationToken)
        => ChangeStockAsync(id, quantity, StockMovementType.Decrement, cancellationToken);

    private async Task<ProductResponse?> ChangeStockAsync(
        int id, int quantity, StockMovementType type, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        if (type == StockMovementType.Decrement && quantity > product.Stock)
        {
            throw new DomainException("There is not enough stock for this operation.");
        }

        // Check before adding, so integer overflow cannot wrap the balance around.
        if (type == StockMovementType.Increment && quantity > int.MaxValue - product.Stock)
        {
            throw new DomainException("The resulting stock would exceed the supported limit.");
        }

        var stockBefore = product.Stock;
        product.Stock = type == StockMovementType.Increment
            ? product.Stock + quantity
            : product.Stock - quantity;

        product.StockMovements.Add(new StockMovement
        {
            Type = type,
            Quantity = quantity,
            StockBefore = stockBefore,
            StockAfter = product.Stock,
            OccurredAtUtc = DateTime.UtcNow
        });

        await _productRepository.SaveChangesAsync(cancellationToken);
        return ToResponse(product);
    }

    private static ProductResponse ToResponse(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Stock = product.Stock,
        CreatedAtUtc = product.CreatedAtUtc
    };
}
