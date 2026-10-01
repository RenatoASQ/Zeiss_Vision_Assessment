namespace Zeiss.Products.Application.Products.Responses;

public record ProductResponse
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int Stock { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
