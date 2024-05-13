using FairwayFinder.Core.Features.CourseManagement.Models.ViewModels;
using FairwayFinder.Core.Features.CourseManagement.Repository;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement.Services;

public interface ICourseManagementService
{
    Task<GetAllCoursesViewModel> GetAllCourses();
    Task<CourseViewModel> GetCourseById(int courseId);
}

public class CourseManagementService(ILogger<CourseManagementService> _logger, ICourseRepository courseRepository) : ICourseManagementService
{

    public async Task<GetAllCoursesViewModel> GetAllCourses()
    {
        var courses = await courseRepository.GetAllCourses();
        return new GetAllCoursesViewModel
        {
            Courses = courses
        };
    }

    public async Task<CourseViewModel> GetCourseById(int courseId)
    {
        var course = await courseRepository.GetCourseById(courseId);
         return new CourseViewModel
        {
            Course = course
        }; 
    }
}