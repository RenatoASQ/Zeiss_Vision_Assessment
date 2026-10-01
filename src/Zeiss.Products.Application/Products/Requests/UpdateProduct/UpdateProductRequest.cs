using FluentValidation;
using Zeiss.Products.Domain.Rules;

namespace Zeiss.Products.Application.Products.Requests.UpdateProduct;

public record UpdateProductRequest
{
    public string Name { get; init; } = string.Empty;

    // Optional. PUT replaces the product, so omitting it, null or blank clears the description.
    public string? Description { get; init; }

    public int? Stock { get; init; }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .Must(name => name.Trim().Length <= ProductRules.MaxNameLength)
            .WithMessage($"Name must not exceed {ProductRules.MaxNameLength} characters.");

        RuleFor(request => request.Description)
            .Must(description => description is null || description.Trim().Length <= ProductRules.MaxDescriptionLength)
            .WithMessage($"Description must not exceed {ProductRules.MaxDescriptionLength} characters.");

        RuleFor(request => request.Stock)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Stock is required.")
            .GreaterThanOrEqualTo(0).WithMessage("Stock must be greater than or equal to zero.");
    }
}
