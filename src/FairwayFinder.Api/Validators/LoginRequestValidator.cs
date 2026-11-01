using FairwayFinder.Api.Auth;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        // Only presence: login checks a password, it does not enforce the policy. A length rule here
        // would answer a short wrong guess with 400 instead of 401, skipping the lockout counter.
        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
