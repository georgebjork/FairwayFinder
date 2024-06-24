using FairwayFinder.Core.Exceptions;
using FairwayFinder.Core.Features.CourseManagement.Services;
using FairwayFinder.Core.Features.Courses.Models.ViewModels;
using FairwayFinder.Core.Services;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.Courses;

public record ViewCourseAllRequest : IRequest<Result<GetAllCoursesViewModel>>
{
    public int CourseId { get; set; }
}

public class ViewAllCourseHandler : IRequestHandler<ViewCourseAllRequest, Result<GetAllCoursesViewModel>>
{
    private readonly ILogger<ViewAllCourseHandler> _logger;
    private readonly ICourseService _courseService;

    public ViewAllCourseHandler(ILogger<ViewAllCourseHandler> logger, ICourseService courseService)
    {
        _logger = logger;
        _courseService = courseService;
    }

    public async Task<Result<GetAllCoursesViewModel>> Handle(ViewCourseAllRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var courses = await _courseService.GetAllCourses();

            return new GetAllCoursesViewModel
            {
                Courses = courses
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occured in ViewAllCoursesHandler Handler.");
            return new Result<GetAllCoursesViewModel>(new Exception("An error occured, unable to get golf courses.", ex));
        }
    }
}