namespace FairwayFinder.Web.Components.Shared.Layout;

public class BreadcrumbItem
{
    public string Text { get; set; } = string.Empty;
    public string? Path { get; set; }

    public BreadcrumbItem() { }

    public BreadcrumbItem(string text, string? path = null)
    {
        Text = text;
        Path = path;
    }
}
