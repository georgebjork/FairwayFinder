using FairwayFinder.Core.Settings;
using FairwayFinder.Web.Controllers.BaseControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = Roles.Admin)]
public class BaseAdminController : BaseAuthorizedController
{
    
}