using FairwayFinder.Features.Data;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class SendTestPushRequestValidator : AbstractValidator<SendTestPushRequest>
{
    public SendTestPushRequestValidator()
    {
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Badge).GreaterThanOrEqualTo(0).When(x => x.Badge.HasValue);
    }
}
