using Zeiss.Products.Domain.Entities;

namespace Zeiss.Products.Application.Products.Interfaces;

public interface IProductRepository
{
    Task<bool> ExistsByNameAsync(string name, int? excludedProductId, CancellationToken cancellationToken);

    Task AddAsync(Product product, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<List<Product>> GetAllAsync(CancellationToken cancellationToken);

    Task<List<Product>> SearchAsync(string name, CancellationToken cancellationToken);

    Task<List<Product>> GetByStockLevelAsync(int min, int max, CancellationToken cancellationToken);
}
