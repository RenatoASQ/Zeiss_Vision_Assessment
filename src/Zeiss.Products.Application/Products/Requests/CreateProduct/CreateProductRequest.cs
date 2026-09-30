using FluentValidation;
using Zeiss.Products.Domain.Rules;

namespace Zeiss.Products.Application.Products.Requests.CreateProduct;

public record CreateProductRequest
{
    public string Name { get; init; } = string.Empty;

    public int Stock { get; init; }
}

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Name is required.")
            .Must(name => name.Trim().Length <= ProductRules.MaxNameLength)
            .WithMessage($"Name must not exceed {ProductRules.MaxNameLength} characters.");

        RuleFor(request => request.Stock)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Stock must be greater than or equal to zero.");
    }
}
