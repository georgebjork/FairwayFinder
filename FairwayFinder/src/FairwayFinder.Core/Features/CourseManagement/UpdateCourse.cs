using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Services;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement;

public record UpdateCourseRequest : IRequest<Result<bool>>
{
    public int CourseId { get; set; }
    public CourseFormModel? Form { get; set; }
}

public class UpdateCourseHandler : IRequestHandler<UpdateCourseRequest, Result<bool>>
{
    private readonly ILogger<UpdateCourseHandler> _logger;
    private readonly ICourseManagementService _courseManagementService;

    public UpdateCourseHandler(ILogger<UpdateCourseHandler> logger, ICourseManagementService courseManagementService)
    {
        _logger = logger;
        _courseManagementService = courseManagementService;
    }

    public async Task<Result<bool>> Handle(UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Form is null) throw new NullReferenceException();

            var rv = await _courseManagementService.UpdateCourse(request.CourseId, request.Form);
            return rv;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occurred in the UpdateCourseHandler");
            return new Result<bool>(new Exception("An error occured, unable to edit golf course", ex)); 
        }
        
        
    }
} 