using System.Globalization;
using FairwayFinder.Landing.Pages;
using FairwayFinder.Landing.Profiles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

// Static site generator: renders each page component to an HTML file and copies wwwroot next to
// them. The output folder is what DigitalOcean App Platform serves (docs/architecture/deployment.md).
//
//   dotnet run --project src/FairwayFinder.Landing -- [outputDir]           generate (default: ./dist)
//   dotnet run --project src/FairwayFinder.Landing -- --serve [outputDir]   generate, then serve it locally
//
// The AppHost starts it with --serve, so the page shows up in the Aspire dashboard. Serving is for
// local preview only; production never runs this process.
//
// Public golf profiles (golf/{slug}/) are fetched from the API when PublicProfiles:ApiUrl and
// PublicProfiles:Slugs are set, in appsettings or as PUBLIC_PROFILE_* variables (see ProfileSource).
// If any fetch fails the build exits non-zero, so App Platform keeps serving the previous deploy.

// Numbers end up in SVG coordinates and copy; never let a build machine's locale write "79,2".
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var serve = args.Contains("--serve");
var outputDir = Path.GetFullPath(args.FirstOrDefault(a => a != "--serve") ?? "dist");

// appsettings beside the build output, then environment variables on top. App Platform's build runs
// without ASPNETCORE_ENVIRONMENT, so it reads appsettings.json only; `dotnet run` and the AppHost run
// as Development and also read appsettings.Development.json.
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

try
{
    await GenerateAsync(outputDir, configuration);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Site generation failed: {ex.Message}");
    return 1;
}

if (!serve)
    return 0;

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
return 0;

static async Task GenerateAsync(string outputDir, IConfiguration configuration)
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
        await WriteAsync(renderer, component, ParameterView.Empty, outputDir, file);

    var assetsDir = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    foreach (var source in Directory.EnumerateFiles(assetsDir, "*", SearchOption.AllDirectories))
    {
        // `dotnet publish` (App Platform's buildpack) adds the Blazor runtime scripts to wwwroot.
        // The site is plain HTML and never loads them.
        var relative = Path.GetRelativePath(assetsDir, source);
        if (relative.StartsWith("_framework" + Path.DirectorySeparatorChar))
            continue;

        var target = Path.Combine(outputDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, overwrite: true);
        Console.WriteLine($"  {Path.GetRelativePath(outputDir, target)}");
    }

    await GenerateProfilesAsync(renderer, outputDir, assetsDir, configuration);

    Console.WriteLine($"Site written to {outputDir}");
}

// golf/{slug}/index.html, rounds/{key}/index.html, badge.svg and card.png for each configured profile.
static async Task GenerateProfilesAsync(HtmlRenderer renderer, string outputDir, string assetsDir, IConfiguration configuration)
{
    var source = ProfileSource.FromConfiguration(configuration);
    if (source is null)
    {
        Console.WriteLine("  (no public profiles: PublicProfiles:ApiUrl or PublicProfiles:Slugs is blank)");
        return;
    }

    foreach (var slug in source.Slugs)
    {
        var profile = await source.FetchAsync(slug);
        var dir = Path.Combine("golf", profile.Slug);
        Directory.CreateDirectory(Path.Combine(outputDir, dir));

        await WriteAsync(renderer, typeof(ProfilePage),
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(ProfilePage.Profile)] = profile }),
            outputDir, Path.Combine(dir, "index.html"));

        foreach (var card in profile.Scorecards ?? [])
        {
            var roundDir = Path.Combine(dir, "rounds", card.Key);
            Directory.CreateDirectory(Path.Combine(outputDir, roundDir));
            await WriteAsync(renderer, typeof(RoundPage),
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(RoundPage.Profile)] = profile,
                    [nameof(RoundPage.Card)] = card
                }),
                outputDir, Path.Combine(roundDir, "index.html"));
        }

        await File.WriteAllTextAsync(Path.Combine(outputDir, dir, "badge.svg"), BadgeSvg.Render(profile));
        Console.WriteLine($"  {Path.Combine(dir, "badge.svg")}");

        await File.WriteAllBytesAsync(Path.Combine(outputDir, dir, "card.png"), OgCardRenderer.Render(profile, assetsDir));
        Console.WriteLine($"  {Path.Combine(dir, "card.png")}");
    }
}

static async Task WriteAsync(HtmlRenderer renderer, Type component, ParameterView parameters, string outputDir, string file)
{
    var html = await renderer.Dispatcher.InvokeAsync(async () =>
    {
        var output = await renderer.RenderComponentAsync(component, parameters);
        return output.ToHtmlString();
    });

    // Razor can't emit a doctype, so it's prepended here.
    await File.WriteAllTextAsync(Path.Combine(outputDir, file), "<!doctype html>\n" + html);
    Console.WriteLine($"  {file}");
}
