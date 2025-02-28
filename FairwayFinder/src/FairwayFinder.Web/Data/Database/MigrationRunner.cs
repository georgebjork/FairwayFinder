using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Web.Data.Database;

public class MigrationRunner
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(ApplicationDbContext dbContext, ILogger<MigrationRunner> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task RunMigrations()
    {
        if ((await _dbContext.Database.GetPendingMigrationsAsync()).Any())
        {
            await _dbContext.Database.MigrateAsync();
        }
        else
        {
            _logger.LogInformation("No pending migrations applied.");
        }
    }
}