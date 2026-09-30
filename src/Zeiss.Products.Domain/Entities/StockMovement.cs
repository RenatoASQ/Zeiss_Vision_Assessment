using Zeiss.Products.Domain.Enums;

namespace Zeiss.Products.Domain.Entities;

public class StockMovement
{
    public long Id { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public StockMovementType Type { get; set; }

    public int Quantity { get; set; }

    public int StockBefore { get; set; }

    public int StockAfter { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}
