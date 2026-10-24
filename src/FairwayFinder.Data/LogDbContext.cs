using FairwayFinder.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FairwayFinder.Data;

/// <summary>
/// The logs database (<c>fairwayfinderlogs</c>): diagnostics written by the system, kept apart from
/// golf data so log volume and retention purges never touch the main database. Same server in
/// production, different database. Migrations live in <c>Migrations/Logs</c>.
/// </summary>
public class LogDbContext(DbContextOptions<LogDbContext> options) : DbContext(options)
{
    public virtual DbSet<AppLogEntry> AppLogs { get; set; }
    public virtual DbSet<ApiRequestLog> ApiRequestLogs { get; set; }

    // This context is what the Serilog database sink writes through. If it logged, a failed write
    // would log "Failed executing DbCommand" at Error, which the sink would try to write, which
    // would fail... Silencing it here rather than in DI keeps it silent for the tests' contexts too.
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseLoggerFactory(NullLoggerFactory.Instance);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // AppLogEntry (Warning+ events from both hosts)
        modelBuilder.Entity<AppLogEntry>(entity =>
        {
            entity.HasKey(e => e.AppLogId).HasName("app_log_pkey");
            entity.ToTable("app_log");
            entity.Property(e => e.AppLogId).HasColumnName("app_log_id");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");
            entity.Property(e => e.Level).HasColumnName("level").HasMaxLength(AppLogColumns.Level);
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.MessageTemplate).HasColumnName("message_template");
            entity.Property(e => e.Exception).HasColumnName("exception");
            entity.Property(e => e.SourceContext).HasColumnName("source_context").HasMaxLength(AppLogColumns.SourceContext);
            entity.Property(e => e.Application).HasColumnName("application").HasMaxLength(AppLogColumns.Application);
            entity.Property(e => e.UserId).HasColumnName("user_id").HasMaxLength(AppLogColumns.UserId);
            entity.Property(e => e.UserEmail).HasColumnName("user_email").HasMaxLength(AppLogColumns.UserEmail);
            entity.Property(e => e.RequestPath).HasColumnName("request_path").HasMaxLength(AppLogColumns.RequestPath);
            entity.Property(e => e.TraceId).HasColumnName("trace_id").HasMaxLength(AppLogColumns.TraceId);
            entity.Property(e => e.Properties).HasColumnName("properties").HasColumnType("jsonb");

            entity.HasIndex(e => e.Timestamp).HasDatabaseName("ix_app_log_timestamp");
            entity.HasIndex(e => e.Level).HasDatabaseName("ix_app_log_level");
            entity.HasIndex(e => e.UserId).HasDatabaseName("ix_app_log_user_id");
            entity.HasIndex(e => e.TraceId).HasDatabaseName("ix_app_log_trace_id");
        });

        // ApiRequestLog (API request audit trail)
        modelBuilder.Entity<ApiRequestLog>(entity =>
        {
            entity.HasKey(e => e.ApiRequestLogId).HasName("api_request_log_pkey");
            entity.ToTable("api_request_log");
            entity.Property(e => e.ApiRequestLogId).HasColumnName("api_request_log_id");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");
            entity.Property(e => e.Method).HasColumnName("method").HasMaxLength(16);
            entity.Property(e => e.Path).HasColumnName("path").HasMaxLength(2048);
            entity.Property(e => e.RouteTemplate).HasColumnName("route_template").HasMaxLength(2048);
            entity.Property(e => e.QueryString).HasColumnName("query_string").HasMaxLength(2048);
            entity.Property(e => e.StatusCode).HasColumnName("status_code");
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
            entity.Property(e => e.UserId).HasColumnName("user_id").HasMaxLength(450);
            entity.Property(e => e.UserEmail).HasColumnName("user_email").HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
            entity.Property(e => e.TraceId).HasColumnName("trace_id").HasMaxLength(128);

            entity.HasIndex(e => e.Timestamp).HasDatabaseName("ix_api_request_log_timestamp");
            entity.HasIndex(e => e.UserId).HasDatabaseName("ix_api_request_log_user_id");
            entity.HasIndex(e => e.StatusCode).HasDatabaseName("ix_api_request_log_status_code");
        });
    }
}

/// <summary>Column lengths for <see cref="AppLogEntry"/>, shared with the sink so it truncates to fit.</summary>
public static class AppLogColumns
{
    public const int Level = 16;
    public const int SourceContext = 512;
    public const int Application = 128;
    public const int UserId = 450;
    public const int UserEmail = 256;
    public const int RequestPath = 2048;
    public const int TraceId = 128;
}
