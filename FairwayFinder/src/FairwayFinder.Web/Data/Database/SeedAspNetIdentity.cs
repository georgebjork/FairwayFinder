using FairwayFinder.Core.Identity;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Web.Data.Database;

public class SeedAspNetIdentity
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public SeedAspNetIdentity(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
    }
    
    
    public async Task CreateRoles() {
       
        var roleNames = Roles.GetAllRoles();

        foreach (var roleName in roleNames) {
            var roleExist = await _roleManager.RoleExistsAsync(roleName);
            // ensure that the role does not exist
            if (!roleExist) {
                //create the roles and seed them to the database: 
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        await AddAdminUser();
    }

    
    // Here you create the super admin who will maintain the web app. This password will obviously be changed. 
    private async Task AddAdminUser() {
        
        var email = _configuration["DefaultUser:Email"];
        var username = _configuration["DefaultUser:Username"];
        var first_name = _configuration["DefaultUser:FirstName"];
        var last_name = _configuration["DefaultUser:LastName"];
        var password = _configuration["DefaultUser:Password"];
        
        // No point of continuing
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(first_name) || string.IsNullOrEmpty(last_name) || string.IsNullOrEmpty(password)) 
        {
            return;
        }
        
        var user = await _userManager.FindByEmailAsync(email);

        // check if the user exists
        if (user == null) {
            //Here you could create the super admin who will maintain the web app
            var seedUser = new ApplicationUser {
                UserName = username,
                Email = email,
                EmailConfirmed = true,
                FirstName = first_name,
                LastName = last_name
            };
            var createPowerUser = await _userManager.CreateAsync(seedUser, password);
            if (createPowerUser.Succeeded) {
                //here we tie the new user to the role
                await _userManager.AddToRoleAsync(seedUser, Roles.Admin);
            }
        }
    }
}