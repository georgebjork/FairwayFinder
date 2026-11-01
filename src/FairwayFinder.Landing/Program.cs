using FairwayFinder.Landing.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

// Static site generator: renders each page component to an HTML file and copies wwwroot next to
// them. The output folder is what DigitalOcean App Platform serves (see Dockerfile).
//
//   dotnet run --project src/FairwayFinder.Landing -- [outputDir]           generate (default: ./dist)
//   dotnet run --project src/FairwayFinder.Landing -- --serve [outputDir]   generate, then serve it locally
//
// The AppHost starts it with --serve, so the page shows up in the Aspire dashboard. Serving is for
// local preview only; production never runs this process.

var serve = args.Contains("--serve");
var outputDir = Path.GetFullPath(args.FirstOrDefault(a => a != "--serve") ?? "dist");

await GenerateAsync(outputDir);

if (!serve)
    return;

var app = WebApplication.CreateBuilder().Build();

// An explicit provider rather than the web root: in Development the Web SDK swaps the web root for
// the project's static web assets, and the default provider hides dot-folders like .well-known.
var site = new PhysicalFileProvider(outputDir, ExclusionFilters.None);

app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = site });
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = site,
    // .well-known/apple-app-site-association has no extension.
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/json",
});

// Mirrors App Platform's error_document. Terminal middleware, not MapFallback: a fallback endpoint
// matches extensionless paths like "/" and wins over the static file middleware.
app.Run(async ctx =>
{
    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
    ctx.Response.ContentType = "text/html";
    await ctx.Response.SendFileAsync(site.GetFileInfo("404.html"));
});

app.Run();

static async Task GenerateAsync(string outputDir)
{
    // Each page and the file it becomes. 404.html is the App Platform error_document.
    var pages = new (Type Component, string File)[]
    {
        (typeof(Home), "index.html"),
        (typeof(NotFound), "404.html"),
    };

    if (Directory.Exists(outputDir))
        Directory.Delete(outputDir, recursive: true);
    Directory.CreateDirectory(outputDir);

    await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
    await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

    foreach (var (component, file) in pages)
    {
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync(component, ParameterView.Empty);
            return output.ToHtmlString();
        });

        // Razor can't emit a doctype, so it's prepended here.
        await File.WriteAllTextAsync(Path.Combine(outputDir, file), "<!doctype html>\n" + html);
        Console.WriteLine($"  {file}");
    }

    var assetsDir = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    foreach (var source in Directory.EnumerateFiles(assetsDir, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(outputDir, Path.GetRelativePath(assetsDir, source));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);
        Console.WriteLine($"  {Path.GetRelativePath(outputDir, target)}");
    }

    Console.WriteLine($"Site written to {outputDir}");
}
