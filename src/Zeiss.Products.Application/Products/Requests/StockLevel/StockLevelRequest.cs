using FluentValidation;

namespace Zeiss.Products.Application.Products.Requests.StockLevel;

public record StockLevelRequest
{
    public int? Min { get; init; }

    public int? Max { get; init; }
}

public class StockLevelRequestValidator : AbstractValidator<StockLevelRequest>
{
    public StockLevelRequestValidator()
    {
        RuleFor(request => request.Min)
            .NotNull().WithMessage("Min is required.")
            .GreaterThanOrEqualTo(0).WithMessage("Min must be greater than or equal to zero.");

        RuleFor(request => request.Max)
            .NotNull().WithMessage("Max is required.")
            .GreaterThanOrEqualTo(0).WithMessage("Max must be greater than or equal to zero.");

        RuleFor(request => request.Max)
            .GreaterThanOrEqualTo(request => request.Min)
            .WithMessage("Max must be greater than or equal to Min.")
            .When(request => request.Min.HasValue && request.Max.HasValue);
    }
}
