using FairwayFinder.Core.Features.GolfCourse.Models;
using FairwayFinder.Core.Features.GolfCourse.Services.Interfaces;
using FairwayFinder.Core.Services;
using FairwayFinder.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Course.Controllers;

[Route("course-stats")]
public class CourseStatsController : BaseCourseController
{
    private readonly ILogger<CourseStatsController> _logger;
    private readonly ICourseStatsService _courseStatsService;
    private readonly ICourseService _courseService;
    private readonly IUsernameRetriever _usernameRetriever;

    public CourseStatsController(ILogger<CourseStatsController> logger, ICourseStatsService courseStatsService, ICourseService courseService, IUsernameRetriever usernameRetriever)
    {
        _logger = logger;
        _courseStatsService = courseStatsService;
        _courseService = courseService;
        _usernameRetriever = usernameRetriever;
    }
    
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetCourseStats([FromQuery] CourseStatsRequest request)
    {
        if (request.CourseId <= 0)
        {
            throw new ArgumentOutOfRangeException($"{nameof(request.CourseId)} must be non null and greater than 0");
        }
        request.UserId = _usernameRetriever.UserId;
        var course = await _courseService.GetCourseByIdAsync(request.CourseId);

        if (course == null)
        {
            SetErrorMessage("Course does not exist");
            return RedirectToAction("Index", "Home");
        }
        
        var course_stats = await _courseStatsService.GetAllCourseStatsAsync(request);

        course_stats.StatsRequest = request;
        return PartialView("CourseStats/CourseStats", course_stats);
    }
}