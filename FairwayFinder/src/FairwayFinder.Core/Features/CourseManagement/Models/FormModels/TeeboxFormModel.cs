namespace FairwayFinder.Core.Features.CourseManagement.Models.FormModels;

public class TeeboxFormModel
{
    public int? TeeboxId { get; set; }
    public int CourseId { get; set; }

    public string Name { get; set; } = "";
    public int Par { get; set; }
    public decimal Rating { get; set; }
    public int Slope { get; set; }
    public int YardageOut { get; set; }
    public int YardageIn { get; set; }
    public int Yardage => YardageIn + YardageOut;
    public bool IsNineHoles { get; set; }
    public bool IsWomenTees { get; set; }
}