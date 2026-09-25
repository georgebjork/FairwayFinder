using FairwayFinder.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FairwayFinder.Data.Interceptors;

/// <summary>
/// Stamps <see cref="IAuditable.CreatedOn"/> and <see cref="IAuditable.UpdatedOn"/> with the
/// current UTC instant on every save, so no service has to remember to do it.
/// </summary>
/// <remarks>
/// Registered from <see cref="ApplicationDbContext.OnConfiguring"/> rather than DI, so it applies to
/// every context however its options were built — including the hand-built in-memory options the
/// test suite uses.
///
/// <c>CreatedBy</c> / <c>UpdatedBy</c> are left alone. Admin pages act on another user's data and
/// must stamp the acting admin, and background jobs have no user at all, so ownership stays an
/// explicit parameter at the call site.
///
/// Note that <c>ExecuteUpdate</c> / <c>ExecuteDelete</c> bypass the change tracker entirely and so
/// bypass this interceptor. Set <c>UpdatedOn</c> by hand in those.
/// </remarks>
public sealed class AuditStampInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Shared instance. This needs to be a singleton: interceptors form part of the
    /// <c>DbContextOptions</c> fingerprint, so handing EF a fresh instance per context would give
    /// every context a distinct fingerprint and a new internal service provider.
    /// </summary>
    public static readonly AuditStampInterceptor Instance = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Kind=Utc: Npgsql accepts nothing else for a timestamptz parameter.
        var utcNow = DateTime.UtcNow;

        // SavingChanges runs before SaveChanges calls DetectChanges, but Entries<T>() detects on the
        // way in, so the states below are accurate. Writing through CurrentValue rather than the
        // POCO property marks each one modified immediately instead of leaning on the later detect.
        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedOn).CurrentValue = utcNow;
                    entry.Property(e => e.UpdatedOn).CurrentValue = utcNow;
                    break;

                case EntityState.Modified:
                    entry.Property(e => e.UpdatedOn).CurrentValue = utcNow;
                    // An update never rewrites the creation instant, whatever the caller did.
                    entry.Property(e => e.CreatedOn).IsModified = false;
                    break;
            }
        }
    }
}
