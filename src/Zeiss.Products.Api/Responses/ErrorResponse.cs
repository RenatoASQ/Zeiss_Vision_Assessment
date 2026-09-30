namespace Zeiss.Products.Api.Responses;

public record ErrorResponse
{
    public string Message { get; init; } = string.Empty;

    public IDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();
}
