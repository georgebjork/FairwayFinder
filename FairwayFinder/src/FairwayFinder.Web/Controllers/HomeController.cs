using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using FairwayFinder.Web.Data.Models;
using Microsoft.AspNetCore.Authorization;

namespace FairwayFinder.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }
        
        [Authorize]
        public IActionResult Index()
        {
            return View();
        }
        
        [Route("/hello")]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}