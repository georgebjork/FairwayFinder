namespace FairwayFinder.Core.Features.Admin.HealthCheck.Models.ViewModels;

public class HealthViewModel
{
    public string? Status { get; set; }
    public string? TotalDuration { get; set; }
    public List<Entry> Entries { get; set; } = [];
}