namespace FairwayFinder.Core.Models.Interfaces;

public interface IEntityMetadata
{
    public string created_by { get; set; }
    public DateTime created_on { get; set; }
    public string updated_by { get; set; }
    public DateTime updated_on { get; set; }
}