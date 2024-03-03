using FairwayFinder.Core.Settings;
using Microsoft.AspNetCore.Authorization;

namespace FairwayFinder.Web.Controllers.BaseControllers;

[Authorize]
public class BaseAuthorizedController : BaseController {
    
}
