using FluentValidation;
using URLShortener.Core.CQRS.Queries;

namespace URLShortener.Core.CQRS.Validators;

public class GetUserUrlsValidator : AbstractValidator<GetUserUrlsQuery>
{
    public GetUserUrlsValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.Skip)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Skip must be non-negative");

        RuleFor(x => x.Take)
            .InclusiveBetween(1, 100)
            .WithMessage("Take must be between 1 and 100");
    }
}

public class SearchUrlsValidator : AbstractValidator<SearchUrlsQuery>
{
    public SearchUrlsValidator()
    {
        RuleFor(x => x.SearchTerm)
            .NotEmpty()
            .WithMessage("Search term is required")
            .MinimumLength(2)
            .WithMessage("Search term must be at least 2 characters")
            .MaximumLength(100)
            .WithMessage("Search term cannot exceed 100 characters");

        RuleFor(x => x.Skip)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Skip must be non-negative");

        RuleFor(x => x.Take)
            .InclusiveBetween(1, 100)
            .WithMessage("Take must be between 1 and 100");
    }
}

public class CheckUrlAvailabilityValidator : AbstractValidator<CheckUrlAvailabilityQuery>
{
    public CheckUrlAvailabilityValidator()
    {
        RuleFor(x => x.ShortCode)
            .NotEmpty()
            .WithMessage("Short code is required")
            .Length(3, 20)
            .WithMessage("Short code must be between 3 and 20 characters")
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Short code can only contain letters, numbers, hyphens, and underscores");
    }
}