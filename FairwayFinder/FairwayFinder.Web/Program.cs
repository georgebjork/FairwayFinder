using FairwayFinder.Core;
using FairwayFinder.Core.Models;
using FairwayFinder.Core.Settings;
using FairwayFinder.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add postgres db context 
var connectionString = builder.Configuration.GetConnectionString("DbConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Authorization config
builder.Services.AddAuthorization();

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

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
    o.Events.OnSigningOut = ctx => {
        ctx.HttpContext.Session.Clear();
        return Task.CompletedTask;
    };
});

builder.Services.AddSession();
builder.Services.AddMvc();
builder.Services.AddControllersWithViews();

ConfigureCoreServices.ConfigureServices(services: builder.Services);

ConfigureSettings(builder.Services, builder.Configuration);

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

app.UseAuthorization();

app.MapGet("/login", context => Task.Factory.StartNew(() => context.Response.Redirect("/Identity/Account/Login", true, true)));
app.MapGet("/register", context => Task.Factory.StartNew(() => context.Response.Redirect("/Identity/Account/Register", true, true)));
app.MapGet("/logout", context => Task.Factory.StartNew(() => context.Response.Redirect("/Identity/Account/Logout", true, true)));
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name : "Admin",
    pattern : "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);
app.MapControllerRoute(
    name : "Profile",
    pattern : "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);


app.MapRazorPages();


app.UseSession();

using (var scope = app.Services.CreateScope()) {
    await RunMigrations(scope.ServiceProvider);
    await CreateRoles(scope.ServiceProvider);
}

app.Run();


void ConfigureSettings(IServiceCollection services, IConfiguration config) {
    services.Configure<SendGridSettings>(config.GetSection("SendGrid"));
}

Task RunMigrations(IServiceProvider serviceProvider)
{
    var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
    if (context.Database.GetPendingMigrations().Any())
    {
        context.Database.Migrate();
    }

    return Task.CompletedTask;
}

async Task CreateRoles(IServiceProvider serviceProvider) {
    //initializing custom roles 
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    string[] roleNames = [Roles.Admin, Roles.User];

    foreach (var roleName in roleNames) {
        var roleExist = await roleManager.RoleExistsAsync(roleName);
        // ensure that the role does not exist
        if (!roleExist) {
            //create the roles and seed them to the database: 
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // Here you create the super admin who will maintain the web app
    await AddAdminUser(userManager, "georgebjork@outlook.com", "password");
}

async Task AddAdminUser(UserManager<ApplicationUser> userManager, string username, string password) {
    var user = await userManager.FindByEmailAsync(username);

    // check if the user exists
    if (user == null) {
        //Here you could create the super admin who will maintain the web app
        var seedUser = new ApplicationUser {
            UserName = username,
            Email = username,
            EmailConfirmed = true
        };
        var createPowerUser = await userManager.CreateAsync(seedUser, password);
        if (createPowerUser.Succeeded) {
            //here we tie the new user to the role
            await userManager.AddToRoleAsync(seedUser, Roles.Admin);
        }
    }
}
