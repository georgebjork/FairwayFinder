using FairwayFinder.Core.Exceptions;
using FairwayFinder.Core.Features.CourseManagement.Services;
using FairwayFinder.Core.Features.Courses.Models.ViewModels;
using FairwayFinder.Core.Services;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Courses;

public record ViewCourseRequest : IRequest<Result<CourseViewModel>>
{
    public int CourseId { get; set; }
}

public class ViewCourseHandler : IRequestHandler<ViewCourseRequest, Result<CourseViewModel>>
{
    private readonly ILogger<ViewCourseHandler> _logger;
    private readonly ICourseService _courseService;
    private readonly ITeeboxManagementService _teeboxManagementService;

    public ViewCourseHandler(ILogger<ViewCourseHandler> logger, ICourseService courseService, ITeeboxManagementService teeboxManagementService)
    {
        _logger = logger;
        _courseService = courseService;
        _teeboxManagementService = teeboxManagementService;
    }

    public async Task<Result<CourseViewModel>> Handle(ViewCourseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var course = await _courseService.GetCourseById(request.CourseId);

            if (course is null) throw new NullCourseException($"Course with id:{request.CourseId} came back null");

            var teeboxes = await _teeboxManagementService.GetTeeboxesByCourseId(request.CourseId);

            return new CourseViewModel
            {
                Course = course,
                Teeboxes = teeboxes
            };
        }
        catch (NullCourseException ex)
        {
            _logger.LogWarning(ex, "Course with id: {courseId} came back null", request.CourseId);
            return new Result<CourseViewModel>(new Exception("Golf course does not exist.", ex));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occured in ViewCourseHandler Handler.");
            return new Result<CourseViewModel>(new Exception("An error occured, unable to get golf course.", ex));
        }
    }
}