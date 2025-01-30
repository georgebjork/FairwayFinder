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
    
    public async Task<Teebox?> GetTeeByIdAsync(long teeboxId)
    {
        return await _courseManagementRepository.GetTeeByIdAsync(teeboxId);
    }
    
    public async Task<List<Teebox>> GetTeeForCourseAsync(long courseId)
    {
        return await _courseManagementRepository.GetTeeForCourseAsync(courseId);
    }

    public async Task<int> AddCourseAsync(CourseFormModel form)
    {
        var course = form.ToModel();
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

        var tee = form.ToModel();
        tee = EntityMetadataHelper.NewRecord(tee, _usernameRetriever.Username);
        return await _courseManagementRepository.Insert(tee);
    }
    
    
    public async Task<bool> UpdateTeeAsync(long courseId, TeeboxFormModel form)
    {
        var course = await GetCourseByIdAsync(courseId);

        if (course is null)
        {
            _logger.LogError("Invalid course id: {0} was used to update a Tee Box id: {1} by user {2}", courseId, form.TeeboxId,  _usernameRetriever.Username);
            return false;
        }
        
        var tee = await GetTeeByIdAsync(form.TeeboxId ?? 0);

        if (tee is null)
        {
            _logger.LogError("Invalid tee box id: {0} was used to update a Tee Box by user {1}", form.TeeboxId, _usernameRetriever.Username);
            return false;
        }
        
        tee.teebox_name = form.Name;
        tee.course_id = courseId;
        tee.par = form.Par;
        tee.slope = form.Slope;
        tee.rating = form.Rating;
        tee.yardage_out = form.Slope;
        tee.yardage_in = form.Slope;
        tee.yardage_total = form.Slope;
        tee.is_nine_hole = form.IsNineHoles;
        tee.is_womens = false;
        
        tee = EntityMetadataHelper.UpdateRecord(tee, _usernameRetriever.Username);
        return await _courseManagementRepository.Update(tee);
    }
}