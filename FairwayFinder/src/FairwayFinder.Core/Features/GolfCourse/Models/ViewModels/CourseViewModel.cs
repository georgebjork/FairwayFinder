using FairwayFinder.Core.Features.GolfCourse.Models.ResponseModels;
using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.GolfCourse.Models.ViewModels;

public class CourseViewModel
{
    public Course Course { get; set; } = new();
    public List<Teebox> Teeboxes { get; set; } = [];
    public CourseStatsResponse CourseStats { get; set; } = new();
}