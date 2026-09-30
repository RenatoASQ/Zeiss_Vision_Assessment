using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeiss.Products.Domain.Entities;

namespace Zeiss.Products.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", "dbo", table =>
        {
            table.HasCheckConstraint("CK_StockMovements_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_StockMovements_Balances_NonNegative",
                "[StockBefore] >= 0 AND [StockAfter] >= 0");
            table.HasCheckConstraint("CK_StockMovements_BalanceChange", """
                ([Type] = N'Increment' AND CONVERT(bigint, [StockAfter]) = CONVERT(bigint, [StockBefore]) + [Quantity])
                OR ([Type] = N'Decrement' AND CONVERT(bigint, [StockAfter]) = CONVERT(bigint, [StockBefore]) - [Quantity])
                OR ([Type] = N'Adjustment' AND [Quantity] = ABS(CONVERT(bigint, [StockAfter]) - CONVERT(bigint, [StockBefore])))
                """);
        });

        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Id).UseIdentityColumn();

        builder.Property(movement => movement.Type)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(movement => movement.OccurredAtUtc)
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        builder.HasOne(movement => movement.Product)
            .WithMany(product => product.StockMovements)
            .HasForeignKey(movement => movement.ProductId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
