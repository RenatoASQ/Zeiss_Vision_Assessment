using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Zeiss.Products.Domain.Entities;
using Zeiss.Products.Domain.Rules;

namespace Zeiss.Products.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Products_Id_Range", "[Id] BETWEEN 100000 AND 999999");
            table.HasCheckConstraint("CK_Products_Stock_NonNegative", "[Stock] >= 0");
        });

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Id)
            .HasDefaultValueSql("NEXT VALUE FOR [dbo].[ProductIds]");

        // CI ignores case; AI ignores accents when comparing product names.
        builder.Property(product => product.Name)
            .IsRequired()
            .HasMaxLength(ProductRules.MaxNameLength)
            .UseCollation("Latin1_General_100_CI_AI");

        // Names can be reused after a product is deleted.
        builder.HasIndex(product => product.Name)
            .IsUnique()
            .HasFilter("[DeletedAtUtc] IS NULL");

        builder.Property(product => product.RowVersion).IsRowVersion();

        // SQL Server datetime2 does not store DateTime.Kind; stored dates are UTC.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            value => value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        builder.Property(product => product.CreatedAtUtc).HasConversion(utcConverter);
        builder.Property(product => product.DeletedAtUtc).HasConversion(utcConverter);
    }
}
