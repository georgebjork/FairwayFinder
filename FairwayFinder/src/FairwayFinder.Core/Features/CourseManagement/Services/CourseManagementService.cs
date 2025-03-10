using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.CourseManagement.Repositories;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement.Services;

public class CourseManagementService
{
    private readonly ILogger<CourseManagementService> _logger;
    private readonly ICourseManagementRepository _courseManagementRepository;
    private readonly IUsernameRetriever _usernameRetriever;
    private readonly CourseLookupService _courseLookupService;
    private readonly TeeboxLookupService _teeboxLookupService;
    private readonly HoleLookupService _holeLookupService;

    public CourseManagementService(ICourseManagementRepository courseManagementRepository, ILogger<CourseManagementService> logger, IUsernameRetriever usernameRetriever, CourseLookupService courseLookupService, TeeboxLookupService teeboxLookupService, HoleLookupService holeLookupService)
    {
        _courseManagementRepository = courseManagementRepository;
        _logger = logger;
        _usernameRetriever = usernameRetriever;
        _courseLookupService = courseLookupService;
        _teeboxLookupService = teeboxLookupService;
        _holeLookupService = holeLookupService;
    }

    public async Task<int> AddCourseAsync(CourseFormModel form)
    {
        var course_with_name = await _courseLookupService.GetCourseByNameAsync(form.name);

        if (course_with_name is not null)
        {
            _logger.LogWarning("Course with name {0} already exists", form.name);
            return -1;
        }
        
        var course = form.ToModel(new Course());
        course = EntityMetadataHelper.NewRecord(course, _usernameRetriever.UserId);
        return await _courseManagementRepository.Insert(course);
    }
    
    public async Task<bool> UpdateCourseAsync(long courseId, CourseFormModel form)
    {
        var course = await _courseLookupService.GetCourseByIdAsync(courseId);
        
        // should never happen here but here in case
        if (course == null) return false;
        
        course.course_name = form.name;
        course.address = form.address;
        course.phone_number = form.phone_number;
        
        course = EntityMetadataHelper.UpdateRecord(course, _usernameRetriever.UserId);
        return await _courseManagementRepository.Update(course);
    }

    public async Task<int> AddTeeAsync(long courseId, TeeboxFormModel form)
    {
        var course = await _courseLookupService.GetCourseByIdAsync(courseId);

        if (course is null)
        {
            _logger.LogError("Invalid course id: {0} was used to create a Tee Box by user {1}", courseId, _usernameRetriever.Username);
        }

        var tee = form.ToModel(new Teebox());
        var holes = form.Holes.Select(hole => EntityMetadataHelper.NewRecord(hole.ToModel(new Hole()), _usernameRetriever.UserId)).ToList();
        tee = EntityMetadataHelper.NewRecord(tee, _usernameRetriever.UserId);
        
        return await _courseManagementRepository.InsertNewTeeAsync(tee, holes);
    }
    
    
    public async Task<bool> UpdateTeeAsync(long teeboxId, TeeboxFormModel form)
    {
        var tee = await _teeboxLookupService.GetTeeByIdAsync(teeboxId);

        if (tee is null)
        {
            _logger.LogError("Invalid tee box id: {0} was used to update a Tee Box by user {1}", teeboxId, _usernameRetriever.Username);
            return false;
        }
        
        var holes = await _holeLookupService.GetHolesForTeeAsync(teeboxId);

        for (var i = 0; i < holes.Count; i++)
        {
            holes[i] = EntityMetadataHelper.UpdateRecord(form.Holes[i].ToModel(holes[i]), _usernameRetriever.UserId);
        }
        
        tee = form.ToModel(tee);
        tee = EntityMetadataHelper.UpdateRecord(tee, _usernameRetriever.UserId);
        
        return await _courseManagementRepository.UpdateTeeAsync(tee, holes);
    }
}