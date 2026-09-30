using FluentValidation;

namespace Zeiss.Products.Application.Products.Requests.ChangeStock;

public record ChangeStockRequest
{
    public int Quantity { get; init; }
}

public class ChangeStockRequestValidator : AbstractValidator<ChangeStockRequest>
{
    public ChangeStockRequestValidator()
    {
        RuleFor(request => request.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.");
    }
}
