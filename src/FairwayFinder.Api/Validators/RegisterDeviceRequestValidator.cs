using FairwayFinder.Features.Data;
using FluentValidation;

namespace FairwayFinder.Api.Validators;

public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.DeviceToken).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DeviceName).MaximumLength(200);
    }
}
