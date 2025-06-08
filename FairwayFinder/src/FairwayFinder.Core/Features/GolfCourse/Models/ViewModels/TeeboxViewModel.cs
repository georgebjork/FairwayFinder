using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.GolfCourse.Models.ViewModels;

public class TeeboxViewModel
{
    public Teebox Teebox { get; set; } = new();
    public Course Course { get; set; } = new();
    public List<Hole> Holes { get; set; } = [];
}