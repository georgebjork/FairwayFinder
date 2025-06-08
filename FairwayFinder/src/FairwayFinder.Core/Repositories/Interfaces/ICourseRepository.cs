using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Repositories.Interfaces;

public interface ICourseRepository : IBaseRepository
{
    public Task<List<Course>> GetAllCoursesAsync();
    public Task<Course?> GetCourseByIdAsync(long courseId);
    public Task<List<Course>> SearchForCourseByNameAsync(string name);
    public Task<Course?> GetCourseByNameAsync(string name);
}