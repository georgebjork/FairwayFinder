using FairwayFinder.Features.Data;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class SendFriendRequestRequestValidator : AbstractValidator<SendFriendRequestRequest>
{
    public SendFriendRequestRequestValidator()
    {
        RuleFor(x => x.AddresseeUserId).NotEmpty();
    }
}
