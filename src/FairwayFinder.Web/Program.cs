using FairwayFinder.Agents;
using FairwayFinder.Data;
using FairwayFinder.Features;
using FairwayFinder.Identity;
using FairwayFinder.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FairwayFinder.Web.Components;
using FairwayFinder.Web.Services;
using FairwayFinder.Web.Startup;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("fairwayfinder") ??
                       throw new InvalidOperationException("Connection string 'fairwayfinder' not found.");

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRadzenComponents();

builder.Services.AddFairwayFinderAuthentication(builder.Configuration);
builder.Services.AddFairwayFinderAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.RegisterSharedSettings(builder.Configuration);
builder.Services.RegisterFeatureServices(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.RegisterWebServices();
builder.Services.AddAgentServices();

builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString,
        name: "FairwayFinderDatabase",
        tags: ["db", "sql", "FairwayFinderAppDb"]);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var startupService = scope.ServiceProvider.GetRequiredService<IApplicationStartupService>();
    await startupService.RunMigrationsAsync();
    await startupService.EnsureRolesExistAsync();
    await startupService.SeedDefaultUserAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Logout endpoint (GET for simple navigation from profile menu)
app.MapGet("/authentication/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
}).RequireAuthorization();

app.Run();