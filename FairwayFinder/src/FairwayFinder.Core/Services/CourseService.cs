using FairwayFinder.Core.Features.GolfCourse.Models.FormModels;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Services;

public class CourseService : ICourseService
{
    private readonly ILogger<CourseService> _logger;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly ICourseRepository _courseRepository;

    public CourseService(ILogger<CourseService> logger, ICourseRepository courseRepository, IUsernameRetriever usernameRetriever)
    {
        _logger = logger;
        _courseRepository = courseRepository;
        _usernameRetriever = usernameRetriever;
    }
    
    public async Task<List<Course>> GetAllCoursesAsync()
    {
        return await _courseRepository.GetAllCoursesAsync();
    }
    public async Task<Course?> GetCourseByIdAsync(long courseId)
    {
        return await _courseRepository.GetCourseByIdAsync(courseId);
    }
    
    public async Task<Course?> GetCourseByNameAsync(string name)
    {
        return await _courseRepository.GetCourseByNameAsync(name);
    }

    public async Task<List<Course>> CourseSearchByName(string name)
    {
        return await _courseRepository.SearchForCourseByNameAsync(name);
    }
    
    public async Task<int> AddCourseAsync(CourseFormModel form)
    {
        var course_with_name = await GetCourseByNameAsync(form.Name);

        if (course_with_name is not null)
        {
            _logger.LogWarning("Course with name {0} already exists", form.Name);
            return -1;
        }
        
        var course = form.ToModel(new Course());
        course = EntityMetadataHelper.NewRecord(course, _usernameRetriever.UserId);
        return await _courseRepository.Insert(course);
    }
    
    public async Task<bool> UpdateCourseAsync(long courseId, CourseFormModel form)
    {
        var course = await GetCourseByIdAsync(courseId);
        
        // should never happen here but here in case
        if (course == null) return false;
        
        course.course_name = form.Name;
        course.address = form.Address;
        course.phone_number = form.PhoneNumber;
        
        course = EntityMetadataHelper.UpdateRecord(course, _usernameRetriever.UserId);
        return await _courseRepository.Update(course);
    }
}