using FluentValidation;
using Zeiss.Products.Domain.Rules;

namespace Zeiss.Products.Application.Products.Requests.SearchProducts;

public record SearchProductsRequest
{
    public string Name { get; init; } = string.Empty;
}

public class SearchProductsRequestValidator : AbstractValidator<SearchProductsRequest>
{
    public SearchProductsRequestValidator()
    {
        RuleFor(request => request.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .Must(name => name.Trim().Length <= ProductRules.MaxNameLength)
            .WithMessage($"Name must not exceed {ProductRules.MaxNameLength} characters.");
    }
}
