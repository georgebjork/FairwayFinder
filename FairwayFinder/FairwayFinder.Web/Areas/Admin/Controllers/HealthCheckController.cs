using FairwayFinder.Core.Features.Admin.HealthCheck.Models;
using FairwayFinder.Core.Features.Admin.HealthCheck.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FairwayFinder.Web.Areas.Admin.Controllers;

public class HealthCheckController(ILogger<HealthCheckController> logger) : BaseAdminController
{
    [Route("/health-check")]
    public IActionResult HealthCheck()
    {
        return View();
    }

    [HttpGet]
    [Route("/get-health")]
    public async Task<IActionResult> GetHealth()
    {
        var healthUrl = $"{RequestUrlBase}/_health"; // Update this with your actual URL
        using var client = new HttpClient();

        var response = await client.GetStringAsync(healthUrl);
        var healthData = JObject.Parse(response);

        // Extract the 'status' and 'totalDuration'
        var status = healthData["status"].ToString();
        var totalDuration = healthData["totalDuration"].ToString();

        // Process entries
        var entries = new List<Entry>();
        var healthEntries = healthData["entries"].ToObject<Dictionary<string, JObject>>();
        foreach (var entry in healthEntries)
        {
            var entryData = new Entry
            {
                Name = entry.Key,
                Status = entry.Value["status"].ToString(),
                JsonData = JsonConvert.SerializeObject(entry.Value, Formatting.Indented)
            };
            entries.Add(entryData);
        }

        // Assuming you have a ViewModel to pass to your View
        var vm = new HealthViewModel
        {
            Status = status,
            TotalDuration = totalDuration,
            Entries = entries
        };

        return PartialView("_HealthCheckDisplay", vm);
    }
}