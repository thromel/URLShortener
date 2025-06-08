using FluentValidation;
using URLShortener.Core.CQRS.Commands;

namespace URLShortener.Core.CQRS.Validators;

public class CreateShortUrlValidator : AbstractValidator<CreateShortUrlCommand>
{
    public CreateShortUrlValidator()
    {
        RuleFor(x => x.OriginalUrl)
            .NotEmpty()
            .WithMessage("Original URL is required")
            .Must(BeAValidUrl)
            .WithMessage("Must be a valid URL")
            .MaximumLength(2048)
            .WithMessage("URL must not exceed 2048 characters");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.CustomAlias)
            .Must(BeValidCustomAlias)
            .When(x => !string.IsNullOrEmpty(x.CustomAlias))
            .WithMessage("Custom alias must be 3-50 characters and contain only letters, numbers, and hyphens");

        RuleFor(x => x.ExpiresAt)
            .Must(BeInTheFuture)
            .When(x => x.ExpiresAt.HasValue)
            .WithMessage("Expiration date must be in the future");

        RuleFor(x => x.Metadata)
            .Must(HaveValidMetadata)
            .When(x => x.Metadata != null)
            .WithMessage("Metadata cannot exceed 10 entries or have keys/values longer than 100 characters");
    }

    private static bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool BeValidCustomAlias(string? alias)
    {
        if (string.IsNullOrEmpty(alias))
            return true;

        return alias.Length >= 3 && 
               alias.Length <= 50 && 
               System.Text.RegularExpressions.Regex.IsMatch(alias, @"^[a-zA-Z0-9-]+$") &&
               !alias.StartsWith('-') && 
               !alias.EndsWith('-') &&
               !alias.Contains("--");
    }

    private static bool BeInTheFuture(DateTime? dateTime)
    {
        return !dateTime.HasValue || dateTime.Value > DateTime.UtcNow;
    }

    private static bool HaveValidMetadata(Dictionary<string, string>? metadata)
    {
        if (metadata == null)
            return true;

        return metadata.Count <= 10 &&
               metadata.All(kv => kv.Key.Length <= 100 && kv.Value.Length <= 100);
    }
}