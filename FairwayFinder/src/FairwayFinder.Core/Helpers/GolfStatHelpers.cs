using FairwayFinder.Core.Features.CourseManagement.Models.FormModels;
using FairwayFinder.Core.Features.Scorecards.Models.FormModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Helpers;

public static class GolfStatHelpers
{
    public static RoundStats GenerateRoundStats(List<HoleScoreFormModel> holes)
    {
        return new RoundStats
        {
            hole_in_one = holes.Count(x => x.Score == 1),
            double_eagles = holes.Count(x => x.Score == x.Par - 3),
            eagles = holes.Count(x => x.Score == x.Par - 2),
            birdies = holes.Count(x => x.Score == x.Par - 1),
            pars = holes.Count(x => x.Score == x.Par),
            bogies = holes.Count(x => x.Score == x.Par + 1),
            double_bogies = holes.Count(x => x.Score == x.Par + 2),
            triple_or_worse = holes.Count(x => x.Score >= x.Par + 3),
        };
    }
    
    public static RoundStats RefreshRoundStats(this RoundStats stats, List<HoleScoreFormModel> holes)
    {
        stats.hole_in_one = holes.Count(x => x.Score == 1);
        stats.double_eagles = holes.Count(x => x.Score == x.Par - 3);
        stats.eagles = holes.Count(x => x.Score == x.Par - 2);
        stats.birdies = holes.Count(x => x.Score == x.Par - 1);
        stats.pars = holes.Count(x => x.Score == x.Par);
        stats.bogies = holes.Count(x => x.Score == x.Par + 1);
        stats.double_bogies = holes.Count(x => x.Score == x.Par + 2);
        stats.triple_or_worse = holes.Count(x => x.Score >= x.Par + 3);

        return stats;
    }
}