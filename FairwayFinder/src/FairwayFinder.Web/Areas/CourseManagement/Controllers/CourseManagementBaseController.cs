using FairwayFinder.Identity.Policy;
using FairwayFinder.Web.Controllers.BaseControllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.CourseManagement.Controllers;

[Area("CourseManagement")]
[Authorize(Policy = Policies.CourseManagement)]
public class CourseManagementBaseController : BaseAuthorizedController
{
    
}