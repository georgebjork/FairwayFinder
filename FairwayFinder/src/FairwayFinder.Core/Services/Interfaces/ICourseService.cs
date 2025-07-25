using FairwayFinder.Core.Features.GolfCourse.Models.FormModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Services.Interfaces;

public interface ICourseService
{
    Task<List<Course>> GetAllCoursesAsync();
    Task<Course?> GetCourseByIdAsync(long courseId);
    Task<Course?> GetCourseByNameAsync(string name);
    Task<List<Course>> CourseSearchByName(string name);
    Task<int> AddCourseAsync(CourseFormModel form);
    Task<bool> UpdateCourseAsync(long courseId, CourseFormModel form);
}
