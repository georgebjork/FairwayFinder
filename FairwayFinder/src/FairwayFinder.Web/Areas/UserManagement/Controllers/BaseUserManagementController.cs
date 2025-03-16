using FairwayFinder.Core.Identity.Settings;
using FairwayFinder.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.UserManagement.Controllers;

[Area("UserManagement")]
public class BaseUserManagementController : BaseAuthorizedController
{
    
}