using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zeiss.Products.Application.Products.Interfaces;
using Zeiss.Products.Infrastructure.Persistence;
using Zeiss.Products.Infrastructure.Repositories;

namespace Zeiss.Products.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ProductsDbContext>(options => options
            .UseSqlServer(connectionString)
            // EF tooling uses the synchronous callback; MigrateAsync uses the asynchronous one.
            .UseSeeding((context, _) => ProductSeeder.Seed((ProductsDbContext)context))
            .UseAsyncSeeding((context, _, cancellationToken) =>
                ProductSeeder.SeedAsync((ProductsDbContext)context, cancellationToken)));
        services.AddScoped<IProductRepository, ProductRepository>();

        return services;
    }
}
