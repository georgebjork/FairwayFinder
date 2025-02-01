using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Repositories;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Services;
using LanguageExt.SomeHelp;
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
    
    public async Task<Teebox?> GetTeeByIdAsync(long teeboxId)
    {
        return await _courseManagementRepository.GetTeeByIdAsync(teeboxId);
    }
    
    public async Task<List<Teebox>> GetTeeForCourseAsync(long courseId)
    {
        return await _courseManagementRepository.GetTeeForCourseAsync(courseId);
    }

    public async Task<List<Hole>> GetHolesForTeeAsync(long teeboxId)
    {
        return await _courseManagementRepository.GetHolesForTeeAsync(teeboxId);
    }

    public async Task<int> AddCourseAsync(CourseFormModel form)
    {
        var course = form.ToModel(new Course());
        course = EntityMetadataHelper.NewRecord(course, _usernameRetriever.Username);
        return await _courseManagementRepository.Insert(course);
    }
    
    public async Task<bool> UpdateCourseAsync(long courseId, CourseFormModel form)
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

    public async Task<int> AddTeeAsync(long courseId, TeeboxFormModel form)
    {
        var course = await GetCourseByIdAsync(courseId);

        if (course is null)
        {
            _logger.LogError("Invalid course id: {0} was used to create a Tee Box by user {1}", courseId, _usernameRetriever.Username);
        }

        var tee = form.ToModel(new Teebox());
        var holes = form.Holes.Select(hole => EntityMetadataHelper.NewRecord(hole.ToModel(new Hole()), _usernameRetriever.Username)).ToList();
        tee = EntityMetadataHelper.NewRecord(tee, _usernameRetriever.Username);
        
        return await _courseManagementRepository.InsertNewTeeAsync(tee, holes);
    }
    
    
    public async Task<bool> UpdateTeeAsync(long teeboxId, TeeboxFormModel form)
    {
        var tee = await GetTeeByIdAsync(teeboxId);

        if (tee is null)
        {
            _logger.LogError("Invalid tee box id: {0} was used to update a Tee Box by user {1}", teeboxId, _usernameRetriever.Username);
            return false;
        }
        
        var holes = await GetHolesForTeeAsync(teeboxId);

        for (int i = 0; i < holes.Count; i++)
        {
            holes[i] = EntityMetadataHelper.UpdateRecord(form.Holes[i].ToModel(holes[i]), _usernameRetriever.Username);
        }
        
        tee = form.ToModel(tee);
        //var holes = form.Holes.Select(hole => EntityMetadataHelper.UpdateRecord(hole.ToModel(), _usernameRetriever.Username)).ToList();
        tee = EntityMetadataHelper.UpdateRecord(tee, _usernameRetriever.Username);
        
        return await _courseManagementRepository.UpdateTeeAsync(tee, holes);
    }
}