using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Services;
using FairwayFinder.Core.Repositories;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement;

public record UpdateTeeboxRequest : IRequest<Result<bool>> 
{
    public int TeeboxId { get; set; }
    public TeeboxFormModel Form { get; set; } = new();
}

public class UpdateTeeboxHandler : IRequestHandler<UpdateTeeboxRequest, Result<bool>> 
{
    private readonly ILogger<UpdateTeeboxHandler> _logger;
    private readonly ITeeboxManagementService _teeboxManagementService;
    
    public UpdateTeeboxHandler(ITeeboxManagementService teeboxManagementService, ILogger<UpdateTeeboxHandler> logger)
    {
        _teeboxManagementService = teeboxManagementService;
        _logger = logger;
    }
    
    public async Task<Result<bool>> Handle(UpdateTeeboxRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Form is null) throw new NullReferenceException();

            var rv = await _teeboxManagementService.UpdateTeebox(request.TeeboxId, request.Form);
            return rv;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred in the UpdateCourseHandler");
            return new Result<bool>(new Exception("An error occured, unable to edit golf course", ex)); 
        }
    }
}
