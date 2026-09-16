using System.Text;
using System.Threading.Channels;
using FairwayFinder.Api.Auth;
using FairwayFinder.Api.BackgroundServices;
using FairwayFinder.Api.Endpoints;
using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.Middleware;
using FairwayFinder.Api.OpenApi;
using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features;
using FairwayFinder.Identity;
using FairwayFinder.ServiceDefaults;
using FairwayFinder.Shared;
using FairwayFinder.Shared.Settings;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── Database ────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("fairwayfinder")
    ?? throw new InvalidOperationException("Connection string 'fairwayfinder' not found.");

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── Identity (same password policy as Web, no cookie config) ─
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── JWT Authentication ──────────────────────────────────────
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;

if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Secret is not configured. Set via user-secrets (dev) or environment variable Jwt__Secret (prod).");

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ── Settings ────────────────────────────────────────────────
builder.Services.RegisterSharedSettings(builder.Configuration);

var apnsSettings = builder.Configuration.GetSection("Apns").Get<ApnsSettings>()!;
if (string.IsNullOrWhiteSpace(apnsSettings.BundleId)
    || string.IsNullOrWhiteSpace(apnsSettings.KeyId)
    || string.IsNullOrWhiteSpace(apnsSettings.TeamId)
    || string.IsNullOrWhiteSpace(apnsSettings.P8Contents))
{
    throw new InvalidOperationException(
        "Apns configuration is incomplete. Set BundleId, KeyId, TeamId, and P8Contents via user-secrets (dev) or environment variables (prod).");
}

// ── Domain Services (reuse existing registration) ───────────
builder.Services.RegisterFeatureServices(builder.Configuration, builder.Environment.IsDevelopment());

// ── Request Logging ─────────────────────────────────────────
var requestLoggingSettings = builder.Configuration.GetSection("RequestLogging").Get<RequestLoggingSettings>()
    ?? new RequestLoggingSettings();
builder.Services.AddSingleton(Channel.CreateBounded<ApiRequestLog>(
    new BoundedChannelOptions(requestLoggingSettings.ChannelCapacity)
    {
        FullMode = BoundedChannelFullMode.DropWrite
    }));
builder.Services.AddScoped<RequestLoggingMiddleware>();
builder.Services.AddHostedService<RequestLogWriter>();
builder.Services.AddHostedService<RequestLogPurgeService>();

// ── Exception Handling ──────────────────────────────────────
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ── Validation ──────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ── OpenAPI ─────────────────────────────────────────────────
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

var app = builder.Build();

app.MapDefaultEndpoints();

// ── Middleware Pipeline ─────────────────────────────────────
app.UseExceptionHandler();

// Log every request (outermost app middleware): sees the final status code and the
// authenticated principal on the way back out.
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("FairwayFinder API")
            .WithTheme(ScalarTheme.BluePlanet)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ───────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapCourseEndpoints();
app.MapRoundEndpoints();
app.MapStatsEndpoints();
app.MapLookupEndpoints();
app.MapProfileEndpoints();
app.MapFriendEndpoints();
app.MapDeviceEndpoints();
app.MapAdminInviteEndpoints();

// ── Public web surface ──────────────────────────────────────
// fairwayfinder.pro points here now that the Blazor web app is gone. These two endpoints are
// all that is left of it: the Apple association file and a landing page for invite links.

// webcredentials: lets credentials saved for fairwayfinder.pro surface on the iOS login screen.
// applinks: routes invitation links (https://fairwayfinder.pro/register?code=...) and password
// reset links straight into the app when it's installed.
app.MapGet("/.well-known/apple-app-site-association", () =>
{
    const string json = """
        {"webcredentials":{"apps":["J2J8V2R7F8.BjorkTech.FairwayFinder-iOS"]},"applinks":{"details":[{"appIDs":["J2J8V2R7F8.BjorkTech.FairwayFinder-iOS"],"components":[{"/":"/register*"},{"/":"/reset-password*"}]}]}}
        """;
    return Results.Content(json, "application/json");
}).AllowAnonymous().ExcludeFromDescription();

// Fallback for invite and reset recipients who do not have the app installed. When the app IS
// installed iOS intercepts these paths via applinks and this never renders.
var appInstallUrl = builder.Configuration["Invites:AppInstallUrl"];
app.MapGet("/register", () => Results.Content(BuildAppLandingPage(appInstallUrl), "text/html"))
    .AllowAnonymous().ExcludeFromDescription();
app.MapGet("/reset-password", () => Results.Content(BuildAppLandingPage(appInstallUrl), "text/html"))
    .AllowAnonymous().ExcludeFromDescription();

app.Run();

static string BuildAppLandingPage(string? appInstallUrl)
{
    var action = string.IsNullOrWhiteSpace(appInstallUrl)
        ? "<p>Install the FairwayFinder app, then open this link again from your device.</p>"
        : $"""<p><a class="cta" href="{appInstallUrl}">Get the app</a></p>""";

    return $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>FairwayFinder</title>
          <style>
            :root { color-scheme: dark; }
            body { margin: 0; min-height: 100vh; display: grid; place-items: center;
                   background: #1e1e2e; color: #cdd6f4; text-align: center;
                   font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", system-ui, sans-serif; }
            main { padding: 2rem; max-width: 30rem; }
            h1 { font-size: 1.75rem; margin: 0 0 .5rem; }
            p { line-height: 1.6; color: #a6adc8; }
            .cta { display: inline-block; margin-top: .5rem; padding: .75rem 1.5rem; border-radius: .5rem;
                   background: #89b4fa; color: #1e1e2e; font-weight: 600; text-decoration: none; }
          </style>
        </head>
        <body>
          <main>
            <h1>FairwayFinder</h1>
            <p>This link opens in the FairwayFinder app.</p>
            {{action}}
          </main>
        </body>
        </html>
        """;
}
