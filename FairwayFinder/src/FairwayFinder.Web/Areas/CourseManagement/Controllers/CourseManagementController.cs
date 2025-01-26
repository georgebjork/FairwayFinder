using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.CourseManagement.Controllers;

public class CourseManagementController : BaseCourseManagementController
{
    private readonly ILogger<CourseManagementController> _logger;

    public CourseManagementController(ILogger<CourseManagementController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }
}