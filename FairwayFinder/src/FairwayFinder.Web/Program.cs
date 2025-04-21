using FairwayFinder.Core;
using FairwayFinder.Core.HttpClients;
using FairwayFinder.Core.HttpClients.UploadThing;
using FairwayFinder.Core.Identity;
using FairwayFinder.Core.Identity.Settings;
using FairwayFinder.Web;
using FairwayFinder.Web.Authorization.Profile;
using FairwayFinder.Web.Authorization.ScorecardManagement;
using Microsoft.AspNetCore.Identity;
using FairwayFinder.Web.Data;
using FairwayFinder.Web.Data.Database;
using Microsoft.AspNetCore.Authentication.Cookies;


var builder = WebApplication.CreateBuilder(args);

// This will get the connection from the appsettings ConnectionStrings
// https://learn.microsoft.com/en-us/dotnet/aspire/database/postgresql-integration?tabs=dotnet-cli
builder.AddNpgsqlDbContext<ApplicationDbContext>("DbConnection");

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddRoles<IdentityRole>()
    .AddDefaultTokenProviders();


// Cookie settings
builder.Services.ConfigureApplicationCookie(o => {
    o.Cookie.Name = ".fairway.finder";
    o.LoginPath = "/login";
    o.LogoutPath = "/logout";
    o.AccessDeniedPath = "/401";
    o.Events.OnSigningOut = (CookieSigningOutContext ctx) => {
        try
        {
            ctx.HttpContext.Session.Clear();
            return Task.CompletedTask;
        }
        catch(Exception ex)
        {
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddDistributedMemoryCache();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policy.CanEditScorecard, policy =>
        policy.Requirements.Add(new CanEditScorecardRequirement()));
    
    options.AddPolicy(Policy.CanEditProfile, policy =>
        policy.Requirements.Add(new CanEditProfileRequirement()));
});



builder.Services.AddSession();
builder.Services.AddMvc();
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

builder.Services.RegisterCoreServices(); // Register services in Core Lib
builder.Services.RegisterWebServices(); // Register services for Web Lib

// HttpClients
builder.Services.AddHttpClient<UploadThingHttpClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/login", context => Task.Factory.StartNew(() => context.Response.Redirect("/Identity/Account/Login", true, true)));
app.MapGet("/logout", context => Task.Factory.StartNew(() => context.Response.Redirect("/Identity/Account/Logout", true, true)));
app.MapControllers();

app.MapControllerRoute(
    name : "UserManagement",
    pattern : "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);

app.MapControllerRoute(
    name : "CourseManagement",
    pattern : "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);

app.MapControllerRoute(
    name : "Scorecards",
    pattern : "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.UseStatusCodePagesWithReExecute("/Error/Error", "?statusCode={0}");
    
app.MapRazorPages();
app.UseSession();


using (var scope = app.Services.CreateScope())
{
    var scopedProvider = scope.ServiceProvider;

    var dbInit = scopedProvider.GetRequiredService<MigrationRunner>();
    await dbInit.RunMigrations();
    
    var userInit = scopedProvider.GetRequiredService<SeedAspNetIdentity>();
    await userInit.CreateRoles();
    
}

app.Run();
