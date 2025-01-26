using FairwayFinder.Core.Identity.Settings;
using FairwayFinder.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.UserManagement.Controllers;

[Area("UserManagement")]
[Authorize(Roles = Roles.Admin)]
public class BaseUserManagementController : BaseController
{
    
}