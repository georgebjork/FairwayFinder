using FairwayFinder.Core.Identity.Settings;
using FairwayFinder.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.Course.Controllers;

[Area("Course")]
[Authorize(Roles = Roles.Admin)]
public class BaseCourseController : BaseAuthorizedController
{
    
}