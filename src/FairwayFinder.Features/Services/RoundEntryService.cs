using System.Diagnostics;
using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Diagnostics;
using FairwayFinder.Features.Enums;
using FairwayFinder.Features.Helpers;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Services;

/// <inheritdoc cref="IRoundEntryService"/>
public class RoundEntryService : IRoundEntryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IRoundService _roundService;
    private readonly IFriendService _friendService;
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<RoundEntryService> _logger;

    public RoundEntryService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IRoundService roundService,
        IFriendService friendService,
        IPushNotificationService pushService,
        ILogger<RoundEntryService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _roundService = roundService;
        _friendService = friendService;
        _pushService = pushService;
        _logger = logger;
    }

    public async Task<RoundEntryResult<StartRoundResponse>> StartRoundAsync(StartRoundRequest request, string userId)
    {
        using var activity = FairwayFinderDiagnostics.RoundsActivity
            .StartActivity(FairwayFinderDiagnostics.ActivityNames.RoundStart);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // One round open at a time. ix_round_user_id_active enforces this in the database too,
        // but checking here lets us hand back the existing round id so the app can offer
        // "resume or discard" instead of a bare conflict.
        var existingRoundId = await dbContext.Rounds
            .Where(r => r.UserId == userId && !r.IsDeleted && !r.IsComplete)
            .Select(r => (long?)r.RoundId)
            .FirstOrDefaultAsync();

        if (existingRoundId is not null)
        {
            return RoundEntryResult<StartRoundResponse>.Fail(
                RoundEntryStatus.ActiveRoundExists, conflictingRoundId: existingRoundId);
        }

        // New rounds may only be started on an active teebox version — same rule the atomic
        // create path applies.
        var teebox = await dbContext.Teeboxes
            .FirstOrDefaultAsync(t => t.TeeboxId == request.TeeboxId && !t.IsDeleted);
        if (teebox is null || teebox.ArchivedOn is not null)
        {
            return RoundEntryResult<StartRoundResponse>.Fail(RoundEntryStatus.TeeboxArchived);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var round = new Round
        {
            CourseId = request.CourseId,
            TeeboxId = request.TeeboxId,
            DatePlayed = request.DatePlayed,
            // Totals stay at zero until holes arrive; every write recomputes them from the
            // scores on record rather than accumulating.
            Score = 0,
            ScoreOut = 0,
            ScoreIn = 0,
            UserId = userId,
            UsingHoleStats = request.UsingHoleStats || request.UsingShotTracking,
            UsingShotTracking = request.UsingShotTracking,
            ExcludeFromStats = false,
            FullRound = request.FullRound,
            FrontNine = request.FrontNine,
            BackNine = request.BackNine,
            // The whole point: this round is not history until the golfer posts it.
            IsComplete = false,
            CreatedBy = userId,
            CreatedOn = today,
            UpdatedBy = userId,
            UpdatedOn = today,
            IsDeleted = false
        };

        dbContext.Rounds.Add(round);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Two devices started a round at the same moment and the index caught the loser.
            // Resolve it the same way as the check above.
            _logger.LogInformation(ex, "Concurrent round start for user {UserId}; returning the existing round", userId);

            var raced = await dbContext.Rounds.AsNoTracking()
                .Where(r => r.UserId == userId && !r.IsDeleted && !r.IsComplete)
                .Select(r => (long?)r.RoundId)
                .FirstOrDefaultAsync();

            return RoundEntryResult<StartRoundResponse>.Fail(
                RoundEntryStatus.ActiveRoundExists, conflictingRoundId: raced);
        }

        var holes = await dbContext.Holes
            .Where(h => h.TeeboxId == request.TeeboxId && !h.IsDeleted)
            .OrderBy(h => h.HoleNumber)
            .Select(h => new HoleInfo
            {
                HoleId = h.HoleId,
                HoleNumber = h.HoleNumber,
                Par = h.Par,
                Yardage = h.Yardage,
                Handicap = h.Handicap
            })
            .ToListAsync();

        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.RoundId, round.RoundId);
        FairwayFinderDiagnostics.RoundsStarted.Add(1, new TagList
        {
            { FairwayFinderDiagnostics.Tags.ShotTracking, request.UsingShotTracking },
            { FairwayFinderDiagnostics.Tags.HoleStats, request.UsingHoleStats }
        });

        return RoundEntryResult<StartRoundResponse>.Ok(new StartRoundResponse
        {
            RoundId = round.RoundId,
            TeeboxId = request.TeeboxId,
            Holes = holes
        });
    }

    public async Task<RoundResponse?> GetActiveRoundAsync(string userId, BaselineLevel level)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var roundId = await dbContext.Rounds
            .Where(r => r.UserId == userId && !r.IsDeleted && !r.IsComplete)
            .Select(r => (long?)r.RoundId)
            .FirstOrDefaultAsync();

        if (roundId is null) return null;

        // GetRoundByIdAsync is deliberately ungated on IsComplete precisely so this works.
        return await _roundService.GetRoundByIdAsync(roundId.Value, level);
    }

    public async Task<RoundEntryResult<RoundProgressResponse>> UpsertHoleAsync(
        long roundId, int holeNumber, UpsertHoleRequest request, string userId)
    {
        using var activity = FairwayFinderDiagnostics.RoundsActivity
            .StartActivity(FairwayFinderDiagnostics.ActivityNames.RoundHoleUpsert);
        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.RoundId, roundId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var (round, guard) = await LoadEditableRoundAsync(dbContext, roundId, userId);
        if (guard is not null) return RoundEntryResult<RoundProgressResponse>.Fail(guard.Value);

        var hole = await ResolveHoleAsync(dbContext, round!.TeeboxId, holeNumber);
        if (hole is null) return RoundEntryResult<RoundProgressResponse>.Fail(RoundEntryStatus.HoleNotOnTeebox);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var transaction = await BeginTransactionAsync(dbContext);

        var score = await dbContext.Scores
            .FirstOrDefaultAsync(s => s.RoundId == roundId && s.HoleId == hole.HoleId && !s.IsDeleted);

        if (score is null)
        {
            score = new Score
            {
                RoundId = roundId,
                HoleId = hole.HoleId,
                HoleScore = request.Score,
                UserId = round.UserId,
                CreatedBy = userId,
                CreatedOn = today,
                UpdatedBy = userId,
                UpdatedOn = today,
                IsDeleted = false
            };
            dbContext.Scores.Add(score);
        }
        else
        {
            // Last write wins — the golfer (or their other device) is correcting the hole.
            score.HoleScore = request.Score;
            score.UpdatedBy = userId;
            score.UpdatedOn = today;
        }

        try
        {
            await dbContext.SaveChangesAsync(); // materialises ScoreId for the child rows
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Two devices wrote this hole for the first time at once. The row the other one
            // inserted is the truth to update; re-read and apply on top of it.
            _logger.LogInformation(ex,
                "Concurrent first write of hole {HoleNumber} on round {RoundId}; retrying as an update",
                holeNumber, roundId);

            dbContext.Entry(score).State = EntityState.Detached;

            score = await dbContext.Scores
                .FirstOrDefaultAsync(s => s.RoundId == roundId && s.HoleId == hole.HoleId && !s.IsDeleted);
            if (score is null) throw;

            score.HoleScore = request.Score;
            score.UpdatedBy = userId;
            score.UpdatedOn = today;
            await dbContext.SaveChangesAsync();
        }

        await WriteHoleDetailAsync(dbContext, round, hole, score, request, userId, today);

        var progress = await RecomputeTotalsAsync(dbContext, round, userId, today);
        await dbContext.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        FairwayFinderDiagnostics.RoundHolesPosted.Add(1, new TagList
        {
            { FairwayFinderDiagnostics.Tags.ShotTracking, round.UsingShotTracking },
            { FairwayFinderDiagnostics.Tags.HoleStats, round.UsingHoleStats }
        });

        progress.HoleNumber = holeNumber;
        progress.HoleId = hole.HoleId;
        progress.ScoreId = score.ScoreId;
        progress.Par = hole.Par;
        progress.Score = request.Score;
        return RoundEntryResult<RoundProgressResponse>.Ok(progress);
    }

    public async Task<RoundEntryResult<RoundProgressResponse>> ClearHoleAsync(long roundId, int holeNumber, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var (round, guard) = await LoadEditableRoundAsync(dbContext, roundId, userId);
        if (guard is not null) return RoundEntryResult<RoundProgressResponse>.Fail(guard.Value);

        var hole = await ResolveHoleAsync(dbContext, round!.TeeboxId, holeNumber);
        if (hole is null) return RoundEntryResult<RoundProgressResponse>.Fail(RoundEntryStatus.HoleNotOnTeebox);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var transaction = await BeginTransactionAsync(dbContext);

        var score = await dbContext.Scores
            .FirstOrDefaultAsync(s => s.RoundId == roundId && s.HoleId == hole.HoleId && !s.IsDeleted);

        if (score is not null)
        {
            // Soft delete keeps the house convention, and the partial index on
            // (round_id, hole_id) filters on is_deleted so the hole can be re-entered later.
            score.IsDeleted = true;
            score.UpdatedBy = userId;
            score.UpdatedOn = today;

            await SoftDeleteHoleChildrenAsync(dbContext, score.ScoreId, userId, today);
            await dbContext.SaveChangesAsync();
        }

        var progress = await RecomputeTotalsAsync(dbContext, round, userId, today);
        await dbContext.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        progress.HoleNumber = holeNumber;
        progress.HoleId = hole.HoleId;
        progress.Par = hole.Par;
        progress.Score = null;
        return RoundEntryResult<RoundProgressResponse>.Ok(progress);
    }

    public async Task<RoundEntryResult<RoundResponse>> CompleteRoundAsync(long roundId, string userId, BaselineLevel level)
    {
        using var activity = FairwayFinderDiagnostics.RoundsActivity
            .StartActivity(FairwayFinderDiagnostics.ActivityNames.RoundComplete);
        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.RoundId, roundId);
        var stopwatch = Stopwatch.StartNew();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var round = await dbContext.Rounds.FirstOrDefaultAsync(r => r.RoundId == roundId && !r.IsDeleted);
        if (round is null) return RoundEntryResult<RoundResponse>.Fail(RoundEntryStatus.RoundNotFound);
        if (round.UserId != userId) return RoundEntryResult<RoundResponse>.Fail(RoundEntryStatus.NotOwner);

        if (round.IsComplete)
        {
            // Idempotent: a retry over patchy signal must not fail, and must not notify twice.
            var already = await _roundService.GetRoundByIdAsync(roundId, level);
            return already is null
                ? RoundEntryResult<RoundResponse>.Fail(RoundEntryStatus.RoundNotFound)
                : RoundEntryResult<RoundResponse>.Ok(already);
        }

        var teeboxIsNineHole = await dbContext.Teeboxes
            .Where(t => t.TeeboxId == round.TeeboxId)
            .Select(t => t.IsNineHole)
            .FirstOrDefaultAsync();

        var entered = await LoadEnteredHolesAsync(dbContext, roundId);
        var holeNumbers = entered.Select(h => h.HoleNumber).ToHashSet();

        var missing = RoundScoringHelper.MissingHoles(holeNumbers, teeboxIsNineHole);
        if (holeNumbers.Count == 0 || missing.Count > 0)
        {
            // A round that is neither a full eighteen nor a complete nine cannot be compared to
            // either, so it is not postable. The client gets the specific holes still needed.
            return RoundEntryResult<RoundResponse>.Fail(
                RoundEntryStatus.RoundIncomplete,
                missingHoles: holeNumbers.Count == 0
                    ? (teeboxIsNineHole ? [.. Enumerable.Range(1, 9)] : [.. Enumerable.Range(1, 18)])
                    : missing);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var transaction = await BeginTransactionAsync(dbContext);

        var totals = RoundScoringHelper.ComputeTotals(entered);
        round.Score = totals.Total;
        round.ScoreOut = totals.ScoreOut;
        round.ScoreIn = totals.ScoreIn;

        // The shape the golfer actually played, which may not be the one they picked at the
        // first tee — the start request is a plan, these holes are the record.
        var shape = RoundScoringHelper.ClassifyShape(holeNumbers, teeboxIsNineHole);
        round.FullRound = shape.FullRound;
        round.FrontNine = shape.FrontNine;
        round.BackNine = shape.BackNine;

        var roundStat = await dbContext.RoundStats
            .FirstOrDefaultAsync(rs => rs.RoundId == roundId && !rs.IsDeleted);

        if (roundStat is null)
        {
            roundStat = new RoundStat
            {
                RoundId = roundId,
                CreatedBy = userId,
                CreatedOn = today,
                UpdatedBy = userId,
                UpdatedOn = today,
                IsDeleted = false
            };
            dbContext.RoundStats.Add(roundStat);
        }
        else
        {
            roundStat.UpdatedBy = userId;
            roundStat.UpdatedOn = today;
        }

        RoundScoringHelper.ApplyScoringDistribution(roundStat, entered);

        round.IsComplete = true;
        round.UpdatedBy = userId;
        round.UpdatedOn = today;

        await dbContext.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();

        await RecordCompletionTelemetryAsync(dbContext, round, holeNumbers.Count, stopwatch);

        // The one place friends hear about the round — never at start, never per hole.
        await RoundNotifications.NotifyFriendsOfNewRoundAsync(
            dbContext, _friendService, _pushService, _logger, round.UserId, round.CourseId, round.Score);

        var response = await _roundService.GetRoundByIdAsync(roundId, level);
        return response is null
            ? RoundEntryResult<RoundResponse>.Fail(RoundEntryStatus.RoundNotFound)
            : RoundEntryResult<RoundResponse>.Ok(response);
    }

    // ── Internals ──

    /// <summary>
    /// Loads a round that is open for hole-by-hole writes, or the reason it is not.
    /// </summary>
    private static async Task<(Round? Round, RoundEntryStatus? Failure)> LoadEditableRoundAsync(
        ApplicationDbContext dbContext, long roundId, string userId)
    {
        var round = await dbContext.Rounds.FirstOrDefaultAsync(r => r.RoundId == roundId && !r.IsDeleted);

        if (round is null) return (null, RoundEntryStatus.RoundNotFound);
        if (round.UserId != userId) return (null, RoundEntryStatus.NotOwner);
        // A posted round is edited as a whole through PUT /api/rounds/{id}; letting the two
        // write models interleave on one round would have them fighting over the same rows.
        if (round.IsComplete) return (null, RoundEntryStatus.RoundAlreadyComplete);

        return (round, null);
    }

    /// <summary>
    /// Finds the hole by its number on the round's own teebox. This is what lets the client send
    /// only a hole number, and what stops a hole from another teebox being written into a round.
    /// </summary>
    private static Task<Hole?> ResolveHoleAsync(ApplicationDbContext dbContext, long teeboxId, int holeNumber)
        => dbContext.Holes.FirstOrDefaultAsync(h =>
            h.TeeboxId == teeboxId && h.HoleNumber == holeNumber && !h.IsDeleted);

    /// <summary>
    /// Writes the shots and hole stats for one hole, following the same rules as the atomic path:
    /// shots win where they can prove a stat, the client supplies only what they cannot.
    /// </summary>
    private static async Task WriteHoleDetailAsync(
        ApplicationDbContext dbContext, Round round, Hole hole, Score score,
        UpsertHoleRequest request, string userId, DateOnly today)
    {
        var holeStat = await dbContext.HoleStats
            .FirstOrDefaultAsync(hs => hs.ScoreId == score.ScoreId && !hs.IsDeleted);

        if (round.UsingShotTracking && request.Shots is { Count: > 0 })
        {
            // Replace the hole's shots outright rather than reconciling positionally. A hole is
            // small, the round is still in progress, and no client holds a ShotId for it yet.
            var existingShots = await dbContext.Shots
                .Where(s => s.ScoreId == score.ScoreId && !s.IsDeleted)
                .ToListAsync();

            foreach (var shot in existingShots)
            {
                shot.IsDeleted = true;
                shot.UpdatedBy = userId;
                shot.UpdatedOn = today;
            }

            dbContext.Shots.AddRange(
                RoundScoringHelper.BuildShots(score.ScoreId, request.Shots, userId, today));

            holeStat = EnsureHoleStat(dbContext, holeStat, score.ScoreId, round.RoundId, hole.HoleId, userId, today);
            RoundScoringHelper.ApplyDerivedHoleStat(holeStat, request.Shots, hole.Par, request);
        }
        else if (round.UsingHoleStats)
        {
            holeStat = EnsureHoleStat(dbContext, holeStat, score.ScoreId, round.RoundId, hole.HoleId, userId, today);
            RoundScoringHelper.ApplyClientHoleStat(holeStat, request);
        }
        else if (holeStat is not null)
        {
            // The round stopped tracking stats, so the row no longer belongs to it.
            holeStat.IsDeleted = true;
            holeStat.UpdatedBy = userId;
            holeStat.UpdatedOn = today;
        }
    }

    private static HoleStat EnsureHoleStat(
        ApplicationDbContext dbContext, HoleStat? existing,
        long scoreId, long roundId, long holeId, string userId, DateOnly today)
    {
        if (existing is not null)
        {
            existing.UpdatedBy = userId;
            existing.UpdatedOn = today;
            return existing;
        }

        var created = new HoleStat
        {
            ScoreId = scoreId,
            RoundId = roundId,
            HoleId = holeId,
            CreatedBy = userId,
            CreatedOn = today,
            UpdatedBy = userId,
            UpdatedOn = today,
            IsDeleted = false
        };

        dbContext.HoleStats.Add(created);
        return created;
    }

    private static async Task SoftDeleteHoleChildrenAsync(
        ApplicationDbContext dbContext, long scoreId, string userId, DateOnly today)
    {
        var holeStats = await dbContext.HoleStats
            .Where(hs => hs.ScoreId == scoreId && !hs.IsDeleted)
            .ToListAsync();
        foreach (var holeStat in holeStats)
        {
            holeStat.IsDeleted = true;
            holeStat.UpdatedBy = userId;
            holeStat.UpdatedOn = today;
        }

        var shots = await dbContext.Shots
            .Where(s => s.ScoreId == scoreId && !s.IsDeleted)
            .ToListAsync();
        foreach (var shot in shots)
        {
            shot.IsDeleted = true;
            shot.UpdatedBy = userId;
            shot.UpdatedOn = today;
        }
    }

    /// <summary>
    /// Every hole on record for the round, as the lines the scoring helpers work in.
    /// </summary>
    private static async Task<List<RoundScoringHelper.HoleScoreLine>> LoadEnteredHolesAsync(
        ApplicationDbContext dbContext, long roundId)
        => await dbContext.Scores
            .Where(s => s.RoundId == roundId && !s.IsDeleted)
            .Join(dbContext.Holes.Where(h => !h.IsDeleted), s => s.HoleId, h => h.HoleId,
                (s, h) => new RoundScoringHelper.HoleScoreLine(h.HoleNumber, s.HoleScore, h.Par))
            .ToListAsync();

    /// <summary>
    /// Recomputes the round's totals from the scores on record. Deriving them rather than
    /// accumulating a delta is what makes the per-hole write idempotent: posting the same hole
    /// twice lands on identical state, and any interleaving corrects itself on the next write.
    /// </summary>
    private static async Task<RoundProgressResponse> RecomputeTotalsAsync(
        ApplicationDbContext dbContext, Round round, string userId, DateOnly today)
    {
        var entered = await LoadEnteredHolesAsync(dbContext, round.RoundId);
        var totals = RoundScoringHelper.ComputeTotals(entered);

        round.Score = totals.Total;
        round.ScoreOut = totals.ScoreOut;
        round.ScoreIn = totals.ScoreIn;
        round.UpdatedBy = userId;
        round.UpdatedOn = today;

        return new RoundProgressResponse
        {
            RoundId = round.RoundId,
            ScoreOut = totals.ScoreOut,
            ScoreIn = totals.ScoreIn,
            Total = totals.Total,
            HolesEntered = totals.HoleCount,
            ParEntered = totals.ParEntered
        };
    }

    private async Task RecordCompletionTelemetryAsync(
        ApplicationDbContext dbContext, Round round, int holeCount, Stopwatch stopwatch)
    {
        var shotCount = 0;
        if (round.UsingShotTracking)
        {
            shotCount = await dbContext.Shots
                .Where(s => !s.IsDeleted)
                .Join(dbContext.Scores.Where(sc => sc.RoundId == round.RoundId && !sc.IsDeleted),
                    s => s.ScoreId, sc => sc.ScoreId, (s, _) => s.ShotId)
                .CountAsync();
        }

        var tags = new TagList
        {
            { FairwayFinderDiagnostics.Tags.Holes, holeCount },
            { FairwayFinderDiagnostics.Tags.ShotTracking, round.UsingShotTracking },
            { FairwayFinderDiagnostics.Tags.HoleStats, round.UsingHoleStats }
        };

        // Counted as created here, not at start: a round becomes real when it is posted, which
        // keeps this counter comparable across the atomic and incremental paths.
        FairwayFinderDiagnostics.RoundsCreated.Add(1, tags);
        if (shotCount > 0) FairwayFinderDiagnostics.ShotsLogged.Add(shotCount, tags);

        FairwayFinderDiagnostics.RoundSaveDuration.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { FairwayFinderDiagnostics.Tags.Holes, holeCount },
            { FairwayFinderDiagnostics.Tags.ShotTracking, round.UsingShotTracking },
            { FairwayFinderDiagnostics.Tags.HoleStats, round.UsingHoleStats },
            { FairwayFinderDiagnostics.Tags.Operation, FairwayFinderDiagnostics.TagValues.OperationComplete }
        });
    }

    /// <summary>
    /// Starts a transaction when the provider has them. The in-memory provider used by the tests
    /// does not, and returns null rather than failing the call.
    /// </summary>
    private static async Task<IDbContextTransaction?> BeginTransactionAsync(ApplicationDbContext dbContext)
        => dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync()
            : null;

    /// <summary>
    /// True when a save failed because it collided with a unique index — the concurrency signal
    /// the per-hole upsert and round start both retry on.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.GetType().Name == "PostgresException"
           && ex.InnerException.GetType().GetProperty("SqlState")?.GetValue(ex.InnerException) as string == "23505";
}
