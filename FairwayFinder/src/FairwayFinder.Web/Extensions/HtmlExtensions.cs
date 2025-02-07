using Humanizer;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FairwayFinder.Web.Extensions;

public static class HtmlExtensions
{
    private static readonly HtmlContentBuilder _emptyBuilder = new HtmlContentBuilder();

    public static IHtmlContent BuildBreadcrumbNavigation(this IHtmlHelper helper)
    {
        // Return an empty builder for the Home or Account controllers.
        if (helper.ViewContext.RouteData.Values["controller"].ToString() == "Home" ||
            helper.ViewContext.RouteData.Values["controller"].ToString() == "Account")
        {
            return _emptyBuilder;
        }

        var controllerName = helper.ViewContext.RouteData.Values["controller"].ToString();
        var actionName = helper.ViewContext.RouteData.Values["action"].ToString();

        var breadcrumb = new HtmlContentBuilder()
            .AppendHtml("<nav aria-label=\"breadcrumb\">")
            .AppendHtml("<ol class=\"breadcrumb\">")
            .AppendHtml("<li class=\"breadcrumb-item\">")
            .AppendHtml(helper.ActionLink("Home", "Index", "Home"))
            .AppendHtml("</li>")
            .AppendHtml("<li class=\"breadcrumb-item\">")
            .AppendHtml(helper.ActionLink(controllerName.Titleize(), "Index", controllerName))
            .AppendHtml("</li>");

        if (!string.Equals(actionName, "Index", StringComparison.OrdinalIgnoreCase))
        {
            breadcrumb.AppendHtml("<li class=\"breadcrumb-item active\" aria-current=\"page\">")
                .Append(actionName.Titleize())
                .AppendHtml("</li>");
        }

        breadcrumb.AppendHtml("</ol></nav>");

        return breadcrumb;
    }

}