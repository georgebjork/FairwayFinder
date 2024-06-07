using FairwayFinder.Core.Features.Profile.Models.ViewModels;
using FairwayFinder.Core.Features.Profile.Services;
using FairwayFinder.Core.Services;
using LanguageExt;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Profile;

public record GetProfileRequest : IRequest<Result<ProfileViewModel>>
{
    public string Username { get; set; } = "";
}

public class GetProfileHandler : IRequestHandler<GetProfileRequest, Result<ProfileViewModel>>
{
    private readonly ILogger<GetProfileHandler> _logger;
    private readonly IProfileService _profileService;
    private readonly IUsernameRetriever _usernameRetriever;

    public GetProfileHandler(IProfileService profileService, ILogger<GetProfileHandler> logger, IUsernameRetriever usernameRetriever)
    {
        _profileService = profileService;
        _logger = logger;
        _usernameRetriever = usernameRetriever;
    }

    public async Task<Result<ProfileViewModel>> Handle(GetProfileRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var vm = await _profileService.GetProfile(request.Username);

            if (vm is not null) return vm;
            
            _logger.LogInformation($"{_usernameRetriever.Username} tried get to user {request.Username} but it came back null.");
            throw new ResultIsNullException("User came back null");
        }
        catch (Exception ex)
        {
            return new Result<ProfileViewModel>(new Exception("User was not found", ex));
        }
    }
}