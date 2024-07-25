using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.Courses.Models.ViewModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement.Services;

public interface ICourseManagementService
{
    Task<int> AddCourse(CourseFormModel form);
    Task<bool> UpdateCourse(int courseId, CourseFormModel form);
    Task<bool> DeleteCourse(int courseId);
}

public class CourseManagementService(ILogger<CourseManagementService> _logger, ICourseRepository courseRepository, IUsernameRetriever usernameRetriever) : ICourseManagementService
{

    public async Task<int> AddCourse(CourseFormModel form)
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

    public async Task<bool> UpdateCourse(int courseId, CourseFormModel form)
    {
        var course = await courseRepository.GetCourseById(courseId);
        
        if (course is null) return false; // Course was not found.

        course.course_name = form.Name;
        course.address = form.Address;
        course.phone_number = form.PhoneNumber;
        course.updated_by = usernameRetriever.Email;
        course.updated_on = DateTime.UtcNow;
        
        return await courseRepository.Update(course);
    }
    
    public async Task<bool> DeleteCourse(int courseId)
    {
        var course = await courseRepository.GetCourseById(courseId);
        
        if (course is null) return false; // Course was not found.

        course.is_deleted = true;
        course.updated_by = usernameRetriever.Email;
        course.updated_on = DateTime.UtcNow;

        return await courseRepository.Update(course);
    }
}