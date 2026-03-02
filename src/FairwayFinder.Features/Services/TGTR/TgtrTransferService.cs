using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data.TGTR;
using FairwayFinder.Features.HttpClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Services.TGTR;

public class TgtrTransferService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly TgtrHttpClient _tgtrHttpClient;
    private readonly ILogger<TgtrTransferService> _logger;

    public TgtrTransferService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        TgtrHttpClient tgtrHttpClient,
        ILogger<TgtrTransferService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _tgtrHttpClient = tgtrHttpClient;
        _logger = logger;
    }

    public async Task<TgtrTransferResult> TransferRoundsAsync(int tgtrPlayerId)
    {
        var result = new TgtrTransferResult();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        // 1. Look up player mapping
        var playerMap = await dbContext.TgtrPlayerMaps
            .FirstOrDefaultAsync(m => m.TgtrPlayerId == tgtrPlayerId && !m.IsDeleted);

        if (playerMap is null)
        {
            result.Errors.Add(new TgtrTransferError
            {
                TgtrRoundId = 0,
                Reason = $"No player mapping found for TGTR player ID {tgtrPlayerId}. Create a TgtrPlayerMap entry first."
            });
            return result;
        }

        var userId = playerMap.UserId;

        // 2. Fetch all rounds from TGTR API
        List<TgtrRoundResponse> tgtrRounds;
        try
        {
            tgtrRounds = await _tgtrHttpClient.GetPlayerRoundsAsync(tgtrPlayerId);
        }
        catch (Exception ex)
        {
            result.Errors.Add(new TgtrTransferError
            {
                TgtrRoundId = 0,
                Reason = $"Failed to fetch rounds from TGTR API: {ex.Message}"
            });
            return result;
        }

        _logger.LogInformation("Fetched {Count} rounds from TGTR for player {TgtrPlayerId}", tgtrRounds.Count, tgtrPlayerId);

        // 3. Load already-imported round IDs to skip duplicates
        var importedTgtrRoundIds = await dbContext.TgtrRoundMaps
            .Where(m => !m.IsDeleted)
            .Select(m => m.TgtrRoundId)
            .ToHashSetAsync();

        // 4. Pre-load existing course and teebox mappings
        var courseMaps = await dbContext.TgtrCourseMaps
            .Where(m => !m.IsDeleted)
            .ToDictionaryAsync(m => m.TgtrCourseId);

        var teeboxMaps = await dbContext.TgtrTeeboxMaps
            .Where(m => !m.IsDeleted)
            .ToDictionaryAsync(m => m.TgtrTeeboxId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 5. Process each round
        foreach (var tgtrRound in tgtrRounds)
        {
            // Skip already imported
            if (importedTgtrRoundIds.Contains(tgtrRound.Id))
            {
                result.RoundsSkipped++;
                continue;
            }

            try
            {
                // Resolve course
                var localCourseId = await ResolveCourseAsync(dbContext, courseMaps, tgtrRound, userId, today);
                if (localCourseId is null)
                {
                    result.Errors.Add(new TgtrTransferError
                    {
                        TgtrRoundId = tgtrRound.Id,
                        Reason = $"No course mapping found and no name match for TGTR course '{tgtrRound.CourseName}' (ID: {tgtrRound.CourseId})"
                    });
                    continue;
                }

                // Resolve teebox
                var localTeeboxId = await ResolveTeeboxAsync(dbContext, teeboxMaps, tgtrRound, localCourseId.Value, userId, today);
                if (localTeeboxId is null)
                {
                    result.Errors.Add(new TgtrTransferError
                    {
                        TgtrRoundId = tgtrRound.Id,
                        Reason = $"No teebox mapping found and no name match for TGTR teebox '{tgtrRound.TeeBoxName}' (ID: {tgtrRound.TeeBoxId}) on course '{tgtrRound.CourseName}'"
                    });
                    continue;
                }

                // Resolve local holes for this teebox
                var localHoles = await dbContext.Holes
                    .Where(h => h.TeeboxId == localTeeboxId.Value && !h.IsDeleted)
                    .ToDictionaryAsync(h => h.HoleNumber);

                if (localHoles.Count == 0)
                {
                    result.Errors.Add(new TgtrTransferError
                    {
                        TgtrRoundId = tgtrRound.Id,
                        Reason = $"No holes found for local teebox ID {localTeeboxId.Value} (TGTR teebox '{tgtrRound.TeeBoxName}')"
                    });
                    continue;
                }

                // Verify all TGTR hole scores can be matched to local holes
                var unmatchedHoles = tgtrRound.HoleScores
                    .Where(hs => !localHoles.ContainsKey(hs.Number))
                    .Select(hs => hs.Number)
                    .ToList();

                if (unmatchedHoles.Count > 0)
                {
                    result.Errors.Add(new TgtrTransferError
                    {
                        TgtrRoundId = tgtrRound.Id,
                        Reason = $"Could not match TGTR hole numbers [{string.Join(", ", unmatchedHoles)}] to local holes for teebox ID {localTeeboxId.Value}"
                    });
                    continue;
                }

                // Determine front/back/full round flags
                var fullRound = tgtrRound.FinishedRound;
                var frontNine = tgtrRound.FinishedFront;
                var backNine = tgtrRound.FinishedBack;

                // Create the Round entity
                var round = new Round
                {
                    CourseId = localCourseId.Value,
                    TeeboxId = localTeeboxId.Value,
                    DatePlayed = DateOnly.FromDateTime(tgtrRound.Date),
                    Score = tgtrRound.Score,
                    ScoreOut = tgtrRound.ScoreOut,
                    ScoreIn = tgtrRound.ScoreIn,
                    UserId = userId,
                    UsingHoleStats = true,
                    ExcludeFromStats = false,
                    FullRound = fullRound,
                    FrontNine = frontNine,
                    BackNine = backNine,
                    CreatedBy = userId,
                    CreatedOn = today,
                    UpdatedBy = userId,
                    UpdatedOn = today,
                    IsDeleted = false
                };

                dbContext.Rounds.Add(round);
                await dbContext.SaveChangesAsync();

                // Create Score entities for each hole
                var scores = tgtrRound.HoleScores.Select(hs => new Score
                {
                    RoundId = round.RoundId,
                    HoleId = localHoles[hs.Number].HoleId,
                    HoleScore = (short)hs.Score,
                    UserId = userId,
                    CreatedBy = userId,
                    CreatedOn = today,
                    UpdatedBy = userId,
                    UpdatedOn = today,
                    IsDeleted = false
                }).ToList();

                dbContext.Scores.AddRange(scores);
                await dbContext.SaveChangesAsync();

                // Create HoleStat entities
                var scoreByHoleId = scores.ToDictionary(s => s.HoleId, s => s.ScoreId);

                var holeStats = tgtrRound.HoleScores.Select(hs =>
                {
                    var localHole = localHoles[hs.Number];
                    return new HoleStat
                    {
                        ScoreId = scoreByHoleId[localHole.HoleId],
                        RoundId = round.RoundId,
                        HoleId = localHole.HoleId,
                        HitFairway = hs.Fairway,
                        HitGreen = hs.Gir,
                        NumberOfPutts = hs.Putts.HasValue ? (short)hs.Putts.Value : null,
                        CreatedBy = userId,
                        CreatedOn = today,
                        UpdatedBy = userId,
                        UpdatedOn = today,
                        IsDeleted = false
                    };
                }).ToList();

                dbContext.HoleStats.AddRange(holeStats);

                // Create RoundStat from TGTR stats
                var roundStat = BuildRoundStat(tgtrRound, round.RoundId, userId, today);
                dbContext.RoundStats.Add(roundStat);

                // Create TgtrRoundMap for duplicate tracking
                var roundMap = new TgtrRoundMap
                {
                    TgtrRoundId = tgtrRound.Id,
                    RoundId = round.RoundId,
                    CreatedBy = userId,
                    CreatedOn = today,
                    UpdatedBy = userId,
                    UpdatedOn = today,
                    IsDeleted = false
                };
                dbContext.TgtrRoundMaps.Add(roundMap);

                await dbContext.SaveChangesAsync();

                // Track this round ID as imported so subsequent iterations skip it if the API returns duplicates
                importedTgtrRoundIds.Add(tgtrRound.Id);
                result.RoundsImported++;

                _logger.LogInformation(
                    "Imported TGTR round {TgtrRoundId} as local round {LocalRoundId} for user {UserId}",
                    tgtrRound.Id, round.RoundId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import TGTR round {TgtrRoundId}", tgtrRound.Id);
                result.Errors.Add(new TgtrTransferError
                {
                    TgtrRoundId = tgtrRound.Id,
                    Reason = $"Unexpected error: {ex.Message}"
                });
            }
        }

        _logger.LogInformation(
            "TGTR transfer complete for player {TgtrPlayerId}: {Imported} imported, {Skipped} skipped, {Errors} errors",
            tgtrPlayerId, result.RoundsImported, result.RoundsSkipped, result.Errors.Count);

        return result;
    }

    private async Task<long?> ResolveCourseAsync(
        ApplicationDbContext dbContext,
        Dictionary<int, TgtrCourseMap> courseMaps,
        TgtrRoundResponse tgtrRound,
        string userId,
        DateOnly today)
    {
        // Check existing mapping first
        if (courseMaps.TryGetValue(tgtrRound.CourseId, out var existingMap))
        {
            return existingMap.CourseId;
        }

        // Try to auto-match by name (case-insensitive)
        var localCourse = await dbContext.Courses
            .FirstOrDefaultAsync(c => c.CourseName.ToLower() == tgtrRound.CourseName.ToLower() && !c.IsDeleted);

        if (localCourse is null)
        {
            return null;
        }

        // Auto-create mapping
        var newMap = new TgtrCourseMap
        {
            TgtrCourseId = tgtrRound.CourseId,
            CourseId = localCourse.CourseId,
            CreatedBy = userId,
            CreatedOn = today,
            UpdatedBy = userId,
            UpdatedOn = today,
            IsDeleted = false
        };

        dbContext.TgtrCourseMaps.Add(newMap);
        await dbContext.SaveChangesAsync();

        // Cache for subsequent rounds in this batch
        courseMaps[tgtrRound.CourseId] = newMap;

        _logger.LogInformation(
            "Auto-mapped TGTR course '{TgtrCourseName}' (ID: {TgtrCourseId}) to local course ID {LocalCourseId}",
            tgtrRound.CourseName, tgtrRound.CourseId, localCourse.CourseId);

        return localCourse.CourseId;
    }

    private async Task<long?> ResolveTeeboxAsync(
        ApplicationDbContext dbContext,
        Dictionary<int, TgtrTeeboxMap> teeboxMaps,
        TgtrRoundResponse tgtrRound,
        long localCourseId,
        string userId,
        DateOnly today)
    {
        // Check existing mapping first
        if (teeboxMaps.TryGetValue(tgtrRound.TeeBoxId, out var existingMap))
        {
            return existingMap.TeeboxId;
        }

        // Try to auto-match by name within the resolved course (case-insensitive)
        var localTeebox = await dbContext.Teeboxes
            .FirstOrDefaultAsync(t => t.CourseId == localCourseId
                                      && t.TeeboxName.ToLower() == tgtrRound.TeeBoxName.ToLower()
                                      && !t.IsDeleted);

        if (localTeebox is null)
        {
            return null;
        }

        // Auto-create mapping
        var newMap = new TgtrTeeboxMap
        {
            TgtrTeeboxId = tgtrRound.TeeBoxId,
            TeeboxId = localTeebox.TeeboxId,
            TgtrCourseId = tgtrRound.CourseId,
            CreatedBy = userId,
            CreatedOn = today,
            UpdatedBy = userId,
            UpdatedOn = today,
            IsDeleted = false
        };

        dbContext.TgtrTeeboxMaps.Add(newMap);
        await dbContext.SaveChangesAsync();

        // Cache for subsequent rounds in this batch
        teeboxMaps[tgtrRound.TeeBoxId] = newMap;

        _logger.LogInformation(
            "Auto-mapped TGTR teebox '{TgtrTeeboxName}' (ID: {TgtrTeeboxId}) to local teebox ID {LocalTeeboxId}",
            tgtrRound.TeeBoxName, tgtrRound.TeeBoxId, localTeebox.TeeboxId);

        return localTeebox.TeeboxId;
    }

    private static RoundStat BuildRoundStat(TgtrRoundResponse tgtrRound, long roundId, string userId, DateOnly today)
    {
        var roundStat = new RoundStat
        {
            RoundId = roundId,
            HoleInOne = 0,
            DoubleEagles = 0,
            Eagles = 0,
            Birdies = 0,
            Pars = 0,
            Bogies = 0,
            DoubleBogies = 0,
            TripleOrWorse = 0,
            CreatedBy = userId,
            CreatedOn = today,
            UpdatedBy = userId,
            UpdatedOn = today,
            IsDeleted = false
        };

        if (tgtrRound.Stats is not null)
        {
            roundStat.Eagles = tgtrRound.Stats.Eagles;
            roundStat.Birdies = tgtrRound.Stats.Birdies;
            roundStat.Pars = tgtrRound.Stats.Pars;
            roundStat.Bogies = tgtrRound.Stats.Bogeys;
            roundStat.DoubleBogies = tgtrRound.Stats.Doubles;
            roundStat.TripleOrWorse = tgtrRound.Stats.Triples + tgtrRound.Stats.Worse;
        }
        else
        {
            // Compute from hole scores if stats not provided
            foreach (var hs in tgtrRound.HoleScores)
            {
                var diff = hs.Score - hs.Par;
                switch (diff)
                {
                    case <= -3:
                        if (hs.Score == 1) roundStat.HoleInOne++;
                        else roundStat.DoubleEagles++;
                        break;
                    case -2:
                        roundStat.Eagles++;
                        break;
                    case -1:
                        roundStat.Birdies++;
                        break;
                    case 0:
                        roundStat.Pars++;
                        break;
                    case 1:
                        roundStat.Bogies++;
                        break;
                    case 2:
                        roundStat.DoubleBogies++;
                        break;
                    default:
                        roundStat.TripleOrWorse++;
                        break;
                }
            }
        }

        return roundStat;
    }

    // --- Mapping CRUD methods for admin UI ---

    public async Task<List<TgtrPlayerMap>> GetPlayerMapsAsync()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.TgtrPlayerMaps
            .Where(m => !m.IsDeleted)
            .OrderBy(m => m.TgtrPlayerId)
            .ToListAsync();
    }

    public async Task<TgtrPlayerMap> AddPlayerMapAsync(int tgtrPlayerId, string userId, string createdBy)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var map = new TgtrPlayerMap
        {
            TgtrPlayerId = tgtrPlayerId,
            UserId = userId,
            CreatedBy = createdBy,
            CreatedOn = today,
            UpdatedBy = createdBy,
            UpdatedOn = today,
            IsDeleted = false
        };

        dbContext.TgtrPlayerMaps.Add(map);
        await dbContext.SaveChangesAsync();
        return map;
    }

    public async Task<List<TgtrCourseMap>> GetCourseMapsAsync()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.TgtrCourseMaps
            .Where(m => !m.IsDeleted)
            .OrderBy(m => m.TgtrCourseId)
            .ToListAsync();
    }

    public async Task<TgtrCourseMap> AddCourseMapAsync(int tgtrCourseId, long courseId, string createdBy)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var map = new TgtrCourseMap
        {
            TgtrCourseId = tgtrCourseId,
            CourseId = courseId,
            CreatedBy = createdBy,
            CreatedOn = today,
            UpdatedBy = createdBy,
            UpdatedOn = today,
            IsDeleted = false
        };

        dbContext.TgtrCourseMaps.Add(map);
        await dbContext.SaveChangesAsync();
        return map;
    }

    public async Task<List<TgtrTeeboxMap>> GetTeeboxMapsAsync()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.TgtrTeeboxMaps
            .Where(m => !m.IsDeleted)
            .OrderBy(m => m.TgtrCourseId)
            .ThenBy(m => m.TgtrTeeboxId)
            .ToListAsync();
    }

    public async Task<TgtrTeeboxMap> AddTeeboxMapAsync(int tgtrTeeboxId, long teeboxId, int tgtrCourseId, string createdBy)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var map = new TgtrTeeboxMap
        {
            TgtrTeeboxId = tgtrTeeboxId,
            TeeboxId = teeboxId,
            TgtrCourseId = tgtrCourseId,
            CreatedBy = createdBy,
            CreatedOn = today,
            UpdatedBy = createdBy,
            UpdatedOn = today,
            IsDeleted = false
        };

        dbContext.TgtrTeeboxMaps.Add(map);
        await dbContext.SaveChangesAsync();
        return map;
    }
}
