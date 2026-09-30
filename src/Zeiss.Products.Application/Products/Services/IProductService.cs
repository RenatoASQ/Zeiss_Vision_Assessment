using Zeiss.Products.Application.Products.Requests.CreateProduct;
using Zeiss.Products.Application.Products.Requests.UpdateProduct;
using Zeiss.Products.Application.Products.Responses;

namespace Zeiss.Products.Application.Products.Services;

public interface IProductService
{
    Task<ProductResponse> CreateAsync(
        CreateProductRequest request, CancellationToken cancellationToken);

    Task<ProductResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<List<ProductResponse>> GetAllAsync(CancellationToken cancellationToken);

    Task<List<ProductResponse>> SearchAsync(string name, CancellationToken cancellationToken);

    Task<List<ProductResponse>> GetByStockLevelAsync(int min, int max, CancellationToken cancellationToken);

    Task<ProductResponse?> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);

    Task<ProductResponse?> AddToStockAsync(int id, int quantity, CancellationToken cancellationToken);

    Task<ProductResponse?> DecrementStockAsync(int id, int quantity, CancellationToken cancellationToken);
}
