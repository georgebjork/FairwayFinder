using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Repositories;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement.Services;

public class CourseManagementService
{
    private readonly ILogger<ICourseManagementRepository> _logger;
    private readonly ICourseManagementRepository _courseManagementRepository;
    private readonly IUsernameRetriever _usernameRetriever;

    public CourseManagementService(ICourseManagementRepository courseManagementRepository, ILogger<ICourseManagementRepository> logger, IUsernameRetriever usernameRetriever)
    {
        _courseManagementRepository = courseManagementRepository;
        _logger = logger;
        _usernameRetriever = usernameRetriever;
    }

    public async Task<List<Course>> GetAllCoursesAsync()
    {
        var courses = await _courseManagementRepository.GetAllAsync();
        return courses;
    }
    
    public async Task<Course?> GetCourseByIdAsync(long courseId)
    {
        return await _courseManagementRepository.GetCourseByIdAsync(courseId);
    }

    public async Task<int> AddCourse(CourseFormModel form)
    {
        var course = new Course
        {
            course_name = form.name,
            address = form.address,
            phone_number = form.phone_number
        };
        course = EntityMetadataHelper.NewRecord(course, _usernameRetriever.Username);
        return await _courseManagementRepository.Insert(course);
    }
    
    public async Task<bool> UpdateCourse(long courseId, CourseFormModel form)
    {
        var course = await GetCourseByIdAsync(courseId);
        
        // should never happen here but here in case
        if (course == null) return false;
        
        course.course_name = form.name;
        course.address = form.address;
        course.phone_number = form.phone_number;
        
        course = EntityMetadataHelper.UpdateRecord(course, _usernameRetriever.Username);
        return await _courseManagementRepository.Update(course);
    }
}