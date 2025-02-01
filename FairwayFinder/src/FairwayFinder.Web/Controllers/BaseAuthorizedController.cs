using Microsoft.AspNetCore.Authorization;

namespace FairwayFinder.Web.Controllers;

[Authorize]
public class BaseAuthorizedController : BaseController
{
    
}