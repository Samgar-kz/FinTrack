using FinTrack.Domain.Common;
using FinTrack.Domain.Users;
using FluentValidation;

namespace FinTrack.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string FullName, string BaseCurrency);

public sealed record LoginRequest(string Email, string Password);

public sealed record UpdateProfileRequest(string FullName, string BaseCurrency);

public sealed record UserDto(Guid Id, string Email, string FullName, UserRole Role, string BaseCurrency);

/// <summary>Result of a successful sign-in or token refresh.</summary>
public sealed record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BaseCurrency)
            .Must(Money.IsSupportedCurrency)
            .WithMessage($"Currency must be one of: {string.Join(", ", Money.SupportedCurrencies)}.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BaseCurrency)
            .Must(Money.IsSupportedCurrency)
            .WithMessage($"Currency must be one of: {string.Join(", ", Money.SupportedCurrencies)}.");
    }
}

internal static class UserMapping
{
    public static UserDto ToDto(this User user) =>
        new(user.Id, user.Email, user.FullName, user.Role, user.BaseCurrency);
}
