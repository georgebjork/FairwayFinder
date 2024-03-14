using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Controllers.BaseControllers;

public class BaseController : Controller {
    
    public string RequestUrlBase => $"{Request.Scheme}://{Request.Host.Value}";

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



    public void SetInfoMessagePersistent(string message)
    {
        TempData["info_message_persistent"] = message;
    }

    public void SetWarningMessagePersistent(string message)
    {
        TempData["warning_message_persistent"] = message;
    }

    public void SetErrorMessagePersistent(string message)
    {
        TempData["error_message_persistent"] = message;
    }
}
