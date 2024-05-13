using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Models.ViewModels;
using FairwayFinder.Core.Features.CourseManagement.Repository;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement.Services;

public interface ICourseManagementService
{
    Task<GetAllCoursesViewModel> GetAllCourses();
    Task<CourseViewModel> GetCourseById(int courseId);

    Task<int> AddCourse(EditCourseFormModel form);
}

public class CourseManagementService(ILogger<CourseManagementService> _logger, ICourseRepository courseRepository, IUsernameRetriever usernameRetriever) : ICourseManagementService
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

    public async Task<int> AddCourse(EditCourseFormModel form)
    {
        try
        {
            var date = DateTime.UtcNow;
            var user = usernameRetriever.Email;

            var course = new Course
            {
                course_name = form.Name,
                address = form.Address,
                phone_number = form.PhoneNumber,
                created_by = user,
                created_on = date,
                updated_by = user,
                updated_on = date
            };

            var rv = await courseRepository.Insert(course);
            return rv;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred creating golf course.");
            return -1;
        }
    }
}