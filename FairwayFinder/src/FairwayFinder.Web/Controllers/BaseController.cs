using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Controllers;

public class BaseController : Controller {
    
    protected string RequestUrlBase => $"{Request.Scheme}://{Request.Host.Value}";
    public void SetInfoMessage(string message) {
        TempData["info_message"] = message;
    }
    public void SetSuccessMessage(string message) {
        TempData["success_message"] = message;
    }
    public void SetWarningMessage(string message) {
        TempData["warning_message"] = message;
    }
    public void SetErrorMessage(string message) {
        TempData["error_message"] = message;
    }
    public void SetSuccessMessageHtmx(string message) {
        TempData["success_message_htmx"] = message;
    }
    public void SetErrorMessageHtmx(string message) {
        TempData["error_message_htmx"] = message;
    }

    public bool CheckHtmxTrigger(string id) => Request.Headers["HX-Trigger"] == id;
    public bool IsHtmx() => Request.Headers["HX-Request"] == "true";
    public void SetHtmxRedirect(string? url) => Response.Headers["HX-Redirect"] = url;
    
    public IActionResult Redirect(string actionName, object? routeValues = null, string? controllerName = null)
    {
        // Build the URL dynamically using Url.Action
        var url = Url.Action(actionName, controllerName, routeValues);

        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("The generated URL cannot be empty", nameof(url));
        }

        if (!IsHtmx())
        {
            // Standard redirect for non-HTMX requests
            return new RedirectResult(url);
        }

        // HTMX-specific redirect: Set the HX-Redirect header
        SetHtmxRedirect(url);
        return Ok();
    }

    public void SendHtmxTriggerAfterSettle(string triggerName)
    {
        var payload = new Dictionary<string, bool> { { triggerName, true } };
        Response.Headers.Append("HX-Trigger-After-Settle", JsonSerializer.Serialize(payload));
    }

}


public static class HtmxTriggers
{
    public static string RenderTable = "RenderTable";
    public static string RenderChart = "RenderChart";
}
