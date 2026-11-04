using FairwayFinder.Data;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Helpers;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FairwayFinder.Features.Services;

public class UsernameService : IUsernameService
{
    // Suffix widths tried in order once the bare base is taken: three 2-digit tries, three 3-digit,
    // then 4-digit. Base (≤ 15) + 4 digits stays inside the 20-char limit.
    private static readonly int[] SuffixDigits = [2, 2, 2, 3, 3, 3, 4, 4, 4, 4, 4, 4];

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public UsernameService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<string> GenerateDefaultAsync(string? firstName, string? lastName)
    {
        var baseName = UsernameRules.BuildBase(firstName, lastName);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        if (await IsFreeAsync(dbContext, baseName))
            return baseName;

        foreach (var digits in SuffixDigits)
        {
            var low = (int)Math.Pow(10, digits - 1);
            var candidate = baseName + Random.Shared.Next(low, low * 10);
            if (await IsFreeAsync(dbContext, candidate))
                return candidate;
        }

        // Twelve misses means the base is extraordinarily popular. A 6-digit suffix on a cut-down
        // base is effectively certain to be free; the unique index is the backstop either way.
        return baseName[..Math.Min(baseName.Length, UsernameRules.MaxLength - 6)]
               + Random.Shared.Next(100_000, 1_000_000);
    }

    public async Task<UsernameAvailability> CheckAvailabilityAsync(string? raw, string? excludingUserId = null)
    {
        var username = UsernameRules.Normalize(raw);

        var error = UsernameRules.Validate(username);
        if (error is not null)
            return new UsernameAvailability { Username = username, Available = false, Reason = error };

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var taken = await dbContext.Users.AsNoTracking()
            .AnyAsync(u => u.Handle == username && u.Id != excludingUserId);

        return new UsernameAvailability
        {
            Username = username,
            Available = !taken,
            Reason = taken ? "That username is taken." : null
        };
    }

    public async Task<UsernameChangeResult> ChangeUsernameAsync(string userId, string? raw)
    {
        var username = UsernameRules.Normalize(raw);

        var error = UsernameRules.Validate(username);
        if (error is not null)
            return new UsernameChangeResult(UsernameChangeStatus.Invalid, username, error);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return new UsernameChangeResult(UsernameChangeStatus.NotFound, username, "User not found.");

        if (user.Handle == username)
            return new UsernameChangeResult(UsernameChangeStatus.Ok, username);

        if (await dbContext.Users.AnyAsync(u => u.Handle == username && u.Id != userId))
            return Taken(username);

        user.Handle = username;
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Someone claimed it between the check and the save.
            return Taken(username);
        }

        return new UsernameChangeResult(UsernameChangeStatus.Ok, username);
    }

    private static UsernameChangeResult Taken(string username) =>
        new(UsernameChangeStatus.Taken, username, "That username is taken.");

    private static async Task<bool> IsFreeAsync(ApplicationDbContext dbContext, string candidate) =>
        !UsernameRules.Reserved.Contains(candidate)
        && !await dbContext.Users.AsNoTracking().AnyAsync(u => u.Handle == candidate);

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is not null
           && ex.InnerException.GetType().GetProperty("SqlState")?.GetValue(ex.InnerException) as string == "23505";
}
