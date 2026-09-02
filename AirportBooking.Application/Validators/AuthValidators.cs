using AirportBooking.Application.DTOs.Auth;
using FluentValidation;

namespace AirportBooking.Application.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private static readonly string[] SupportedLanguages = ["en", "fr"];

    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(64);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(64);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256);

        // Mirrors the Identity password policy configured in Infrastructure. Both
        // are needed: this one produces a helpful field-level message, Identity's
        // is the rule that actually gates account creation.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(10).WithMessage("Use at least 10 characters.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Include at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Include at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Include at least one digit.");

        RuleFor(x => x.PreferredLanguage)
            .Must(language => SupportedLanguages.Contains(language))
            .WithMessage("Language must be 'en' or 'fr'.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Only presence is checked. Validating the shape of a submitted password
        // would tell an attacker which guesses were even worth making.
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
