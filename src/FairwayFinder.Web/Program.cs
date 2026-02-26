using FairwayFinder.Data;
using FairwayFinder.Features.HttpClients;
using FairwayFinder.Features.Services;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Features.Services.TGTR;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FairwayFinder.Web.Components;
using FairwayFinder.Web.Components.Auth;
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
builder.Services.AddScoped<IdentityRedirectManager>();

// Feature services
builder.Services.AddTransient<IRoundService, RoundService>();
builder.Services.AddTransient<IStatsService, StatsService>();
builder.Services.AddTransient<ICourseService, CourseService>();

// TGTR integration
builder.Services.AddHttpClient<TgtrHttpClient>(client =>
{
    var baseUrl = builder.Configuration["Tgtr:BaseUrl"]
                  ?? throw new InvalidOperationException("Tgtr:BaseUrl configuration is missing.");
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddTransient<TgtrTransferService>();

// App Stuff
builder.Services.AddTransient<IApplicationRoleService, ApplicationRoleService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleService = scope.ServiceProvider.GetRequiredService<IApplicationRoleService>();
    await roleService.EnsureRolesExistAsync();
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