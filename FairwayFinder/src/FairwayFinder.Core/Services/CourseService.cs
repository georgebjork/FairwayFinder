using FairwayFinder.Core.Features.CourseManagement.Services;
using FairwayFinder.Core.Features.Courses.Models.ViewModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Services;

public interface ICourseService
{
    Task<List<Course>> GetAllCourses();
    Task<Course?> GetCourseById(int courseId);

}

public class CourseService(ILogger<CourseManagementService> _logger, ICourseRepository courseRepository, IUsernameRetriever usernameRetriever) : ICourseService
{
    public async Task<List<Course>> GetAllCourses()
    {
        var courses = await courseRepository.GetAllCourses();
        return courses;
    }

    public async Task<Course?> GetCourseById(int courseId)
    {
        var course = await courseRepository.GetCourseById(courseId);
        return course;
    }
}