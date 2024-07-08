using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Services;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement;

public record CreateCourseRequest : IRequest<Result<int>> 
{
    public CourseFormModel Form { get; set; } = new();
}

public class CreateCourseHandler : IRequestHandler<CreateCourseRequest, Result<int>> {
    
    private readonly ILogger<CreateCourseHandler> _logger;
    private readonly ICourseManagementService _courseManagementService;

    public CreateCourseHandler(ILogger<CreateCourseHandler> logger, ICourseManagementService courseManagementService)
    {
        _logger = logger;
        _courseManagementService = courseManagementService;
    }
    
    public async Task<Result<int>> Handle(CreateCourseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Form is null) throw new NullReferenceException("Form object was null.");
            return await _courseManagementService.AddCourse(request.Form);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occured in CreateCourse Handler.");
            return new Result<int>(new Exception("An error occured, unable to create course", ex));
        }
    }
}
