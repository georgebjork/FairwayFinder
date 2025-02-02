using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Repositories.Interfaces;

public interface ICourseLookupRepository : IBaseRepository
{
    public Task<List<Course>> GetAllCoursesAsync();
    public Task<Course?> GetCourseByIdAsync(long courseId);
    public Task<List<Course>> CourseSearchByName(string name);
}