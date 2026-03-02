namespace FairwayFinder.Web.Components.Shared.Layout;

public class BreadcrumbState
{
    private List<BreadcrumbItem> _items = [];

    public IReadOnlyList<BreadcrumbItem> Items => _items;

    public event Action? OnChange;

    public void Set(params BreadcrumbItem[] items)
    {
        _items = [..items];
        OnChange?.Invoke();
    }

    public void Reset()
    {
        _items = [];
        OnChange?.Invoke();
    }
}
