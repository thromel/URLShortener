using FluentValidation;
using URLShortener.Core.CQRS.Commands;

namespace URLShortener.Core.CQRS.Validators;

public class DisableUrlValidator : AbstractValidator<DisableUrlCommand>
{
    public DisableUrlValidator()
    {
        RuleFor(x => x.ShortCode)
            .NotEmpty()
            .WithMessage("Short code is required")
            .Length(3, 20)
            .WithMessage("Short code must be between 3 and 20 characters")
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Short code can only contain letters, numbers, hyphens, and underscores");

        RuleFor(x => x.Reason)
            .IsInEnum()
            .WithMessage("Invalid disable reason");

        RuleFor(x => x.AdminNotes)
            .MaximumLength(500)
            .WithMessage("Admin notes cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.AdminNotes));

        RuleFor(x => x.AdminUserId)
            .NotEmpty()
            .WithMessage("Admin user ID is required");
    }
}

public class DeleteUrlValidator : AbstractValidator<DeleteUrlCommand>
{
    public DeleteUrlValidator()
    {
        RuleFor(x => x.ShortCode)
            .NotEmpty()
            .WithMessage("Short code is required")
            .Length(3, 20)
            .WithMessage("Short code must be between 3 and 20 characters")
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Short code can only contain letters, numbers, hyphens, and underscores");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}