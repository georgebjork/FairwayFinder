using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
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
    private readonly ITeeboxService _teeboxService;
    private readonly IHoleService _holeService;
    
    private readonly ICourseRepository _courseRepository;

    public CourseService(ILogger<CourseService> logger, ICourseRepository courseRepository, IUsernameRetriever usernameRetriever, IHoleService holeService, ITeeboxService teeboxService)
    {
        _logger = logger;
        _courseRepository = courseRepository;
        _usernameRetriever = usernameRetriever;
        _holeService = holeService;
        _teeboxService = teeboxService;
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
        var course_with_name = await GetCourseByNameAsync(form.name);

        if (course_with_name is not null)
        {
            _logger.LogWarning("Course with name {0} already exists", form.name);
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
        
        course.course_name = form.name;
        course.address = form.address;
        course.phone_number = form.phone_number;
        
        course = EntityMetadataHelper.UpdateRecord(course, _usernameRetriever.UserId);
        return await _courseRepository.Update(course);
    }

    public async Task<int> AddTeeAsync(long courseId, TeeboxFormModel form)
    {
        var course = await GetCourseByIdAsync(courseId);

        if (course is null)
        {
            _logger.LogError("Invalid course id: {0} was used to create a Tee Box by user {1}", courseId, _usernameRetriever.Username);
        }

        var tee = form.ToModel(new Teebox());
        var holes = form.Holes.Select(hole => EntityMetadataHelper.NewRecord(hole.ToModel(new Hole()), _usernameRetriever.UserId)).ToList();
        tee = EntityMetadataHelper.NewRecord(tee, _usernameRetriever.UserId);
        
        return await _courseRepository.InsertNewTeeAsync(tee, holes);
    }
    
    
    public async Task<bool> UpdateTeeAsync(long teeboxId, TeeboxFormModel form)
    {
        var tee = await _teeboxService.GetTeeByIdAsync(teeboxId);

        if (tee is null)
        {
            _logger.LogError("Invalid tee box id: {0} was used to update a Tee Box by user {1}", teeboxId, _usernameRetriever.Username);
            return false;
        }
        
        var holes = await _holeService.GetHolesForTeeAsync(teeboxId);

        for (var i = 0; i < holes.Count; i++)
        {
            holes[i] = EntityMetadataHelper.UpdateRecord(form.Holes[i].ToModel(holes[i]), _usernameRetriever.UserId);
        }
        
        tee = form.ToModel(tee);
        tee = EntityMetadataHelper.UpdateRecord(tee, _usernameRetriever.UserId);
        
        return await _courseRepository.UpdateTeeAsync(tee, holes);
    }
}