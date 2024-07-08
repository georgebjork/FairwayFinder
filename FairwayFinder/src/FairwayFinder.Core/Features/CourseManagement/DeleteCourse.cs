using FairwayFinder.Core.Features.CourseManagement.Services;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement;

public record DeleteCourseRequest : IRequest<Result<bool>> 
{
    public int CourseId { get; set; }
}

public class DeleteCourseHandler : IRequestHandler<DeleteCourseRequest, Result<bool>> {

    private readonly ILogger<DeleteCourseHandler> _logger;
    private readonly ICourseManagementService _courseManagementService;

    public DeleteCourseHandler(ILogger<DeleteCourseHandler> logger, ICourseManagementService courseManagementService)
    {
        _logger = logger;
        _courseManagementService = courseManagementService;
    }
    
    public async Task<Result<bool>> Handle(DeleteCourseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.CourseId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.CourseId), "A non-valid id was passed.");
            }
            
            // Delete course.
            var result = await _courseManagementService.DeleteCourse(request.CourseId);

            if (!result)
            {
                throw new InvalidOperationException("Failed to delete the course.");
            }

            return result;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "Invalid CourseId in DeleteCourse Handler.");
            return new Result<bool>(ex);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Course deletion failed in DeleteCourse Handler.");
            return new Result<bool>(ex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An unexpected error occurred in DeleteCourse Handler.");
            return new Result<bool>(new Exception("An error occurred, unable to delete course", ex));
        }
    }
}
