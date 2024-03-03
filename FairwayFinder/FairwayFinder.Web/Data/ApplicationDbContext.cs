using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Web.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        
        // Add the custom fields to the identity user
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<ApplicationUser>().Property(e => e.FirstName).HasMaxLength(250);
            modelBuilder.Entity<ApplicationUser>().Property(e => e.LastName).HasMaxLength(250);
        }
    }

    public class ApplicationUser : IdentityUser
    {
        public string FirstName = "";
        public string LastName = "";
    }
    
}
