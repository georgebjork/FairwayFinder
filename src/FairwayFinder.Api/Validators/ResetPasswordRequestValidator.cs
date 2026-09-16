using FairwayFinder.Api.Auth;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Token)
            .NotEmpty();

        // Mirrors the Identity password policy configured in Program.cs.
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8);
    }
}
