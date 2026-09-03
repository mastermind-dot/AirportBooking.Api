using AirportBooking.Application.DTOs.Auth;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace AirportBooking.Application.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private static readonly string[] SupportedLanguages = ["en", "fr"];

    /// <summary>
    /// The localizer is resolved per request, so messages follow the culture
    /// that RequestLocalizationMiddleware set from Accept-Language. Building the
    /// strings in the constructor would be wrong: validators are registered
    /// scoped, but the message must reflect the culture of the request being
    /// validated, not of the first one that happened to construct it.
    /// </summary>
    public RegisterRequestValidator(IStringLocalizer<ValidationMessages> text)
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage(_ => text["Required.FirstName"])
            .MaximumLength(64);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage(_ => text["Required.LastName"])
            .MaximumLength(64);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(_ => text["Required.Email"])
            .EmailAddress().WithMessage(_ => text["Invalid.Email"])
            .MaximumLength(256);

        // Mirrors the Identity password policy configured in Infrastructure.
        // Both are needed: this one produces a helpful field-level message,
        // Identity's is the rule that actually gates account creation.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(_ => text["Required.Password"])
            .MinimumLength(10).WithMessage(_ => text["Password.TooShort"])
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage(_ => text["Password.NeedsUpper"])
            .Matches("[a-z]").WithMessage(_ => text["Password.NeedsLower"])
            .Matches("[0-9]").WithMessage(_ => text["Password.NeedsDigit"]);

        RuleFor(x => x.PreferredLanguage)
            .Must(language => SupportedLanguages.Contains(language))
            .WithMessage(_ => text["Language.Unsupported"]);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        // Only presence is checked, and deliberately without custom messages:
        // describing the shape of a submitted password would tell an attacker
        // which guesses were even worth making.
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
