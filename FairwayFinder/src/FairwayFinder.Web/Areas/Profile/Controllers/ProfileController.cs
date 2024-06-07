using FairwayFinder.Core.Features.Profile;
using FairwayFinder.Core.Features.Profile.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Profile.Controllers;

public class ProfileController(IMediator mediator) : BaseProfileController
{
    [Route("/profile/{userName}")]
    public async Task<IActionResult> ViewProfile([FromRoute] string userName)
    {
        var result = await mediator.Send(new GetProfileRequest { Username = userName });

        return result.Match<IActionResult>(
            View,
            err => {
                SetErrorMessage($"{err.Message}");
                return RedirectToAction("Index", "Home");
            }
        );
    }
}