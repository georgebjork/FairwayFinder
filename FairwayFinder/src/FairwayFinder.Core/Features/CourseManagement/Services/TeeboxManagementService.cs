using FairwayFinder.Core.Exceptions;
using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement.Services;

public interface ITeeboxManagementService
{
    public Task<int> CreateTeebox(TeeboxFormModel form);
    public Task<List<Teebox>> GetTeeboxesByCourseId(long courseId);
    public Task<bool> UpdateTeebox(long teeboxId, TeeboxFormModel form);
    public Task<bool> DeleteTeebox(long teeboxId);
}

public class TeeboxManagementService(ILogger<TeeboxManagementService> logger, IUsernameRetriever usernameRetriever, ITeeboxRepository teeboxRepository) : ITeeboxManagementService
{
    public async Task<int> CreateTeebox(TeeboxFormModel form)
    {
        try
        {
            var date = DateTime.UtcNow;
            var user = usernameRetriever.Email;

            var teebox = new Teebox
            {
                course_id = form.CourseId,
                teebox_name = form.Name,
                par = form.Par,
                rating = form.Rating,
                slope = form.Slope,
                yardage_out = form.YardageOut,
                yardage_in = form.YardageIn ?? 0,
                yardage_total = form.Yardage,
                is_nine_hole = form.IsNineHoles,
                is_womens = form.IsWomenTees,
                created_by = user,
                created_on = date,
                updated_by = user,
                updated_on = date
            };

            var rv = await teeboxRepository.Insert(teebox);
            return rv;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred creating golf course.");
            return -1;
        }
    }
    public async Task<bool> UpdateTeebox(long teeboxId, TeeboxFormModel form)
    {
        if (teeboxId <= 0) throw new ArgumentOutOfRangeException(nameof(teeboxId));

        var teebox = await teeboxRepository.GetTeeboxById(teeboxId);

        if (teebox is null) throw new NullTeeboxException($"Teebox with id {teeboxId} came back null");

        teebox.teebox_name = form.Name;
        teebox.par = form.Par;
        teebox.slope = form.Slope;
        teebox.rating = form.Rating;
        teebox.yardage_out = form.YardageOut;
        teebox.yardage_in = form.YardageIn ?? 0;
        teebox.yardage_total = form.Yardage;
        teebox.is_nine_hole = form.IsNineHoles;
        teebox.is_womens = form.IsWomenTees;
        teebox.updated_by = usernameRetriever.Email;
        teebox.updated_on = DateTime.UtcNow;

        return await teeboxRepository.Update(teebox);
    }
    public async Task<bool> DeleteTeebox(long teeboxId)
    {
        var teebox = await teeboxRepository.GetTeeboxById(teeboxId);

        if (teebox is null) throw new NullTeeboxException($"Teebox with id {teeboxId} came back null");

        teebox.is_deleted = true;
        teebox.updated_by = usernameRetriever.Email;
        teebox.updated_on = DateTime.UtcNow;

        return await teeboxRepository.Update(teebox);
    }

    public async Task<List<Teebox>> GetTeeboxesByCourseId(long courseId)
    {
        return await teeboxRepository.GetTeeboxesByCourseId(courseId);
    }
}