﻿using FairwayFinder.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FairwayFinder.Web.Areas.CourseManagement.Controllers;

[Area("CourseManagement")]
[Authorize(Roles = Roles.Admin)]
public class BaseCourseManagementController : BaseAuthorizedController
{
    
}