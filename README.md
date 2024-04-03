# FairwayFinder

## Tech Stack/Prerequisites
- .NET 8
- Dotnet EF (https://learn.microsoft.com/en-us/ef/core/cli/dotnet)
- Postgres 16 + pgadmin
- Docker
- Visual Studio/JetBrains Rider 

## Local development 
To start devoloping locally, please make sure you have the above prerequisites installed and ready to go. 

### Getting Started
NOTE: You will need SendGrid credentials provided by the SendGrid admin for certain features such as health checks and user invites to work properly on your machine.

In pgadmin, create a database called FairwayFinder

Open the solution in your editor of choice. You should see this project strucure:

- src
  - FairwayFinder.Core
  - FairwayFinder.Web
- tests
  - FairwayFinder.Core.Tests
 
Enter into the FairwayFinder.Web project and go to `Program.cs`

At the bottom you should see this code block. Replace the email, firstname, and lastname in this code block to be your own information. This will set up a local dev admin account.
```
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

    // Here you create the super admin who will maintain the web app. This password will obviously be changed. 
    await AddAdminUser(userManager, "georgebjork@outlook.com", "password");
}

// This is used on an initial launch of the application.
async Task AddAdminUser(UserManager<ApplicationUser> userManager, string username, string password) {
    var user = await userManager.FindByEmailAsync(username);

    // check if the user exists
    if (user == null) {
        //Here you could create the super admin who will maintain the web app
        var seedUser = new ApplicationUser {
            UserName = username,
            Email = username,
            EmailConfirmed = true,
            FirstName = "George",
            LastName = "Bjork"
        };
        var createPowerUser = await userManager.CreateAsync(seedUser, password);
        if (createPowerUser.Succeeded) {
            //here we tie the new user to the role
            await userManager.AddToRoleAsync(seedUser, Roles.Admin);
        }
    }
}
```

Once down, open FairwayFinder.Web in your terminal and run this command, 
```
dotnet ef database update
```
to run all pending migrations for your db. Refresh your db to see if the migrations ran. If they did, you should be able to run the application.
