using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Zeiss.Products.Application.Products.Interfaces;
using Zeiss.Products.Domain.Entities;
using Zeiss.Products.Domain.Exceptions;
using Zeiss.Products.Infrastructure.Persistence;

namespace Zeiss.Products.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ProductsDbContext _context;

    public ProductRepository(ProductsDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsByNameAsync(string name, int? excludedProductId, CancellationToken cancellationToken)
    {
        // SQL Server uses the Name column's case-insensitive, accent-insensitive collation.
        return _context.Products.AnyAsync(
            product => product.DeletedAtUtc == null && product.Name == name
                && (excludedProductId == null || product.Id != excludedProductId), cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        _context.Products.Add(product);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // EF saves product changes and any new movements in one transaction.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // The row changed after it was read. Do not retry with stale business decisions.
            throw new DomainException("The product changed during this operation. Read it again before retrying.", exception);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
            && sqlException.Message.Contains("IX_Products_Name", StringComparison.Ordinal))
        {
            // A concurrent request can insert this name after the service's existence check.
            throw new DomainException("A product with this name already exists.", exception);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 11728 })
        {
            // SQL Server reports 11728 when a non-cycling sequence runs out of values.
            throw new DomainException("The product identifier range is exhausted.", exception);
        }
    }

    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        // Tracking preserves the original RowVersion and detects changes on SaveChanges.
        return _context.Products.SingleOrDefaultAsync(
            product => product.Id == id && product.DeletedAtUtc == null, cancellationToken);
    }

    public Task<List<Product>> GetAllAsync(CancellationToken cancellationToken)
    {
        return _context.Products.AsNoTracking()
            .Where(product => product.DeletedAtUtc == null)
            .OrderBy(product => product.Id).ToListAsync(cancellationToken);
    }

    public Task<List<Product>> SearchAsync(string name, CancellationToken cancellationToken)
    {
        return _context.Products.AsNoTracking()
            .Where(product => product.DeletedAtUtc == null && product.Name.Contains(name))
            .OrderBy(product => product.Id).ToListAsync(cancellationToken);
    }

    public Task<List<Product>> GetByStockLevelAsync(int min, int max, CancellationToken cancellationToken)
    {
        return _context.Products.AsNoTracking()
            .Where(product => product.DeletedAtUtc == null && product.Stock >= min && product.Stock <= max)
            .OrderBy(product => product.Id).ToListAsync(cancellationToken);
    }
}
