using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.Courses.Models.ViewModels;

public class CourseViewModel
{
    public Core.Models.Course? Course { get; set; } = new();
    public List<Teebox> Teeboxes { get; set; } = [];
}