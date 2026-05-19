using System.Text;
using FairwayFinder.Api.Auth;
using FairwayFinder.Api.Endpoints;
using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.OpenApi;
using FairwayFinder.Data;
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

app.Run();
