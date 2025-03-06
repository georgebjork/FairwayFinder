using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Services;

public class CourseLookupService
{
    private readonly ILogger<CourseLookupService> _logger;
    private readonly ICourseLookupRepository _courseLookupRepository;

    public CourseLookupService(ILogger<CourseLookupService> logger, ICourseLookupRepository courseLookupRepository)
    {
        _logger = logger;
        _courseLookupRepository = courseLookupRepository;
    }
    
    
    public async Task<List<Course>> GetAllCoursesAsync()
    {
        return await _courseLookupRepository.GetAllCoursesAsync();
    }
    public async Task<Course?> GetCourseByIdAsync(long courseId)
    {
        return await _courseLookupRepository.GetCourseByIdAsync(courseId);
    }
    
    public async Task<Course?> GetCourseByNameAsync(string name)
    {
        return await _courseLookupRepository.GetCourseByNameAsync(name);
    }

    public async Task<List<Course>> CourseSearchByName(string name)
    {
        return await _courseLookupRepository.SearchForCourseByNameAsync(name);
    }
}