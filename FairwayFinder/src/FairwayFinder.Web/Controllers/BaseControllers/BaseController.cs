using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Controllers.BaseControllers;

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
    
    public new IActionResult Redirect([StringSyntax(StringSyntaxAttribute.Uri)] string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("Url cannot be empty", nameof(url));
        }

        if (!IsHtmx()) return new RedirectResult(url);
        
        SetHtmxRedirect(url);
        return Ok();
    }
}
