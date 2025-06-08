using FairwayFinder.Core.Models;

namespace FairwayFinder.Core.Features.GolfCourse.Models.ViewModels;

public class CourseManagementViewModel
{
    public List<Course> Courses { get; set; } = [];
    
    public List<Teebox> Teeboxes { get; set; } = [];
}