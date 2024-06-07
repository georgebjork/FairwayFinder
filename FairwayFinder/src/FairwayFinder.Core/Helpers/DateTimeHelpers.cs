namespace FairwayFinder.Core.Helpers;

public static class DateTimeHelpers
{
    public static string FormatDate(this DateTime date) 
    {
        return date.Year <= 1970 ? "" : date.ToString("M/d/yy");
    }
    
}