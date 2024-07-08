using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Repositories;
using FairwayFinder.Core.Services;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Core.Features.CourseManagement.Services;

public interface ITeeboxManagementService
{
    public Task<int> CreateTeebox(TeeboxFormModel form);
    public Task<List<Teebox>> GetTeeboxesByCourseId(int courseId);
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
                par = form.Par ?? 0,
                rating = form.Rating ?? 0,
                slope = form.Slope ?? 0,
                yardage_out = form.YardageOut ?? 0,
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

    public async Task<List<Teebox>> GetTeeboxesByCourseId(int courseId)
    {
        return await teeboxRepository.GetTeeboxesByCourseId(courseId);
    }
}