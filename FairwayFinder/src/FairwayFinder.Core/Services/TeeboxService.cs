using FairwayFinder.Core.Features.GolfCourse.Models.FormModels;
using FairwayFinder.Core.Helpers;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories.Interfaces;
using FairwayFinder.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Services;

public class TeeboxService : ITeeboxService
{
    private readonly ILogger<TeeboxService> _logger;
    private readonly ICourseService _courseService;
    private readonly IHoleService _holeService;
    private readonly ITeeboxRepository _teeboxRepository;
    private readonly IUsernameRetriever _usernameRetriever;

    public TeeboxService(ILogger<TeeboxService> logger, ITeeboxRepository teeboxRepository, ICourseService courseService, IUsernameRetriever usernameRetriever, IHoleService holeService)
    {
        _logger = logger;
        _teeboxRepository = teeboxRepository;
        _courseService = courseService;
        _usernameRetriever = usernameRetriever;
        _holeService = holeService;
    }
    
    public async Task<Teebox?> GetTeeByIdAsync(long teeboxId)
    {
        return await _teeboxRepository.GetTeeByIdAsync(teeboxId);
    }
     
    public async Task<List<Teebox>> GetTeesForCourseAsync(long courseId)
    {
        return await _teeboxRepository.GetTeesForCourseAsync(courseId);
    }
    
    public async Task<Dictionary<string, string>> GetTeesDropdownForCourseAsync(long courseId)
    {
        return await _teeboxRepository.GetTeesDropdownForCourseAsync(courseId);
    }
    
    public async Task<int> AddTeeAsync(long courseId, TeeboxFormModel form)
    {
        var course = await _courseService.GetCourseByIdAsync(courseId);

        if (course is null)
        {
            _logger.LogError("Invalid course id: {0} was used to create a Tee Box by user {1}", courseId, _usernameRetriever.Username);
        }

        var tee = form.ToModel(new Teebox());
        var holes = form.Holes.Select(hole => EntityMetadataHelper.NewRecord(hole.ToModel(new Hole()), _usernameRetriever.UserId)).ToList();
        tee = EntityMetadataHelper.NewRecord(tee, _usernameRetriever.UserId);
        
        return await _teeboxRepository.InsertNewTeeAsync(tee, holes);
    }
    
    
    public async Task<bool> UpdateTeeAsync(long teeboxId, TeeboxFormModel form)
    {
        var tee = await GetTeeByIdAsync(teeboxId);

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
        
        return await _teeboxRepository.UpdateTeeAsync(tee, holes);
    }
}