namespace Zeiss.Products.Domain.Entities;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Stock { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public List<StockMovement> StockMovements { get; set; } = [];
}
