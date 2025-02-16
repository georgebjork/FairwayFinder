using Microsoft.AspNetCore.Mvc.Rendering;

namespace FairwayFinder.Web.Helpers;

public static class DictionaryToSelectListItemsHelper
{

    public static List<SelectListItem> ToSelectList(this Dictionary<int, string> dict, string chooseListText = "", string chooseListValue = "") {
        var rv = dict.Keys.Select(key => new SelectListItem(dict[key], key.ToString())).ToList();
        if (!string.IsNullOrWhiteSpace(chooseListText)) {
            rv.Insert(0, new SelectListItem(chooseListText, chooseListValue));
        }
        return rv;
    }
    
    public static List<SelectListItem> ToSelectList(this Dictionary<long, string> dict, string chooseListText = "", string chooseListValue = "") {
        var rv = dict.Keys.Select(key => new SelectListItem(dict[key], key.ToString())).ToList();
        if (!string.IsNullOrWhiteSpace(chooseListText)) {
            rv.Insert(0, new SelectListItem(chooseListText, chooseListValue));
        }
        return rv;
    }

    public static List<SelectListItem> ToSelectList(this Dictionary<string, string> dict, string chooseListText = "", string chooseListValue = "")
    {
        var rv = dict.Keys.Select(key => new SelectListItem(dict[key], key)).ToList();
        if (!string.IsNullOrWhiteSpace(chooseListText))
        {
            rv.Insert(0, new SelectListItem(chooseListText, chooseListValue));
        }
        return rv;
    }

    public static List<SelectListItem> ToSelectListWithEmptyFirst(this Dictionary<int, string> dict)
    {
        var rv = dict.Keys.Select(key => new SelectListItem(dict[key], key.ToString())).ToList();
        rv.Insert(0, new SelectListItem(string.Empty, string.Empty));
        return rv;
    }
}