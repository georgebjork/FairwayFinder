using FairwayFinder.Core.Features.CourseManagement;
using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Services;
using FairwayFinder.Core.Services;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement;

public record CreateTeeboxRequest : IRequest<Result<int>>
{
    public TeeboxFormModel? Form { get; set; }
}

public class CreateTeeboxHandler : IRequestHandler<CreateTeeboxRequest, Result<int>>
{
    private readonly ILogger<CreateTeeboxHandler> _logger;
    private readonly ITeeboxManagementService _teeboxManagementService;

    public CreateTeeboxHandler(ILogger<CreateTeeboxHandler> logger, ITeeboxManagementService teeboxManagementService)
    {
        _logger = logger;
        _teeboxManagementService = teeboxManagementService;
    }

    public async Task<Result<int>> Handle(CreateTeeboxRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Form is null) throw new NullReferenceException("Form object was null.");
            return await _teeboxManagementService.CreateTeebox(request.Form);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occured in CreateTeebox Handler.");
            return new Result<int>(new Exception("An error occured, unable to create teebox", ex));
        }
    }
}