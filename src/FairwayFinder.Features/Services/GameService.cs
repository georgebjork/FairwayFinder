using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using FairwayFinder.Data;
using FairwayFinder.Data.Entities;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Diagnostics;
using FairwayFinder.Features.Games;
using FairwayFinder.Features.Helpers;
using FairwayFinder.Features.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FairwayFinder.Features.Services;

/// <inheritdoc cref="IGameService"/>
public class GameService : IGameService
{
    /// <summary>
    /// Join-code alphabet. No I, O, 0 or 1 — the code gets read aloud across a tee box.
    /// </summary>
    private const string JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private const int JoinCodeLength = 6;

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IGameScoringEngineResolver _engineResolver;
    private readonly GameScoreReader _reader;
    private readonly IFriendService _friendService;
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<GameService> _logger;

    public GameService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IGameScoringEngineResolver engineResolver,
        GameScoreReader reader,
        IFriendService friendService,
        IPushNotificationService pushService,
        ILogger<GameService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _engineResolver = engineResolver;
        _reader = reader;
        _friendService = friendService;
        _pushService = pushService;
        _logger = logger;
    }

    // ── Create and read ──

    public async Task<GameResult<GameStateResponse>> CreateGameAsync(CreateGameRequest request, string hostUserId)
    {
        using var activity = FairwayFinderDiagnostics.GamesActivity
            .StartActivity(FairwayFinderDiagnostics.ActivityNames.GameCreate);
        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.GameType, request.GameType.ToString());

        if (!_engineResolver.Supports(request.GameType))
        {
            return Fail(GameResultStatus.GameTypeNotSupported, $"{request.GameType} cannot be scored yet.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var holeNumbers = ShapeHoleNumbers(request.FullRound, request.FrontNine, request.BackNine);

        var teeboxCheck = await ValidateTeeboxAsync(dbContext, request.TeeboxId, request.CourseId, holeNumbers, allowArchived: false);
        if (teeboxCheck is not null) return teeboxCheck;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var displayName = await ResolveDisplayNameAsync(dbContext, hostUserId);

        // The game and its host participant are one unit: a game with nobody in it is not a
        // thing a host can recover from, and the join code would sit on it holding a live slot.
        await using var transaction = await BeginTransactionAsync(dbContext);

        var game = new Game
        {
            GameType = request.GameType,
            CourseId = request.CourseId,
            DatePlayed = request.DatePlayed,
            HostUserId = hostUserId,
            State = GameState.Setup,
            FullRound = request.FullRound,
            FrontNine = request.FrontNine,
            BackNine = request.BackNine,
            UseNet = request.UseNet,
            HandicapAllowancePercent = request.HandicapAllowancePercent,
            StrokesOffLow = request.StrokesOffLow,
            SkinsCarryover = request.SkinsCarryover,
            SkinsValue = request.SkinsValue,
            CreatedBy = hostUserId,
            CreatedOn = today,
            UpdatedBy = hostUserId,
            UpdatedOn = today
        };

        await SaveWithFreshJoinCodeAsync(dbContext, game);

        var participant = new GameParticipant
        {
            GameId = game.GameId,
            UserId = hostUserId,
            DisplayName = displayName,
            TeeboxId = request.TeeboxId,
            CourseHandicap = request.CourseHandicap,
            Team = request.Team,
            CreatedBy = hostUserId,
            CreatedOn = today,
            UpdatedBy = hostUserId,
            UpdatedOn = today
        };

        dbContext.GameParticipants.Add(participant);
        await dbContext.SaveChangesAsync();

        // The host may link a round they have already started.
        if (request.RoundId is { } roundId)
        {
            var link = await ApplyRoundLinkAsync(dbContext, game, participant, roundId, hostUserId, today);
            if (link is not null) return link;

            await dbContext.SaveChangesAsync();
        }

        if (transaction is not null) await transaction.CommitAsync();

        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.GameId, game.GameId);

        FairwayFinderDiagnostics.GamesCreated.Add(1, new TagList
        {
            { FairwayFinderDiagnostics.Tags.GameType, game.GameType.ToString() }
        });

        return await BuildStateAsync(game.GameId);
    }

    public async Task<List<GameSummaryResponse>> GetMyGamesAsync(string userId, bool activeOnly)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var gameIds = await dbContext.GameParticipants.AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => p.GameId)
            .Distinct()
            .ToListAsync();

        var query = dbContext.Games.AsNoTracking().Where(g => gameIds.Contains(g.GameId) && !g.IsDeleted);

        if (activeOnly)
        {
            query = query.Where(g => g.State == GameState.Setup || g.State == GameState.Active);
        }

        var games = await query
            .OrderByDescending(g => g.DatePlayed).ThenByDescending(g => g.GameId)
            .Select(g => new
            {
                g.GameId,
                g.GameType,
                g.State,
                g.CourseId,
                CourseName = g.Course.CourseName,
                g.DatePlayed,
                g.JoinCode,
                g.HostUserId
            })
            .ToListAsync();

        // One grouped aggregate for the whole list rather than a count query per row.
        var counts = await dbContext.GameParticipants.AsNoTracking()
            .Where(p => gameIds.Contains(p.GameId) && !p.IsDeleted)
            .GroupBy(p => p.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToListAsync();

        var countMap = counts.ToDictionary(c => c.GameId, c => c.Count);

        return
        [
            .. games.Select(g => new GameSummaryResponse
            {
                GameId = g.GameId,
                GameType = g.GameType,
                State = g.State,
                CourseId = g.CourseId,
                CourseName = g.CourseName,
                DatePlayed = g.DatePlayed,
                JoinCode = g.JoinCode,
                IsHost = g.HostUserId == userId,
                ParticipantCount = countMap.TryGetValue(g.GameId, out var c) ? c : 0
            })
        ];
    }

    public async Task<GameResult<GameStateResponse>> GetGameAsync(long gameId, string? userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var game = await LoadGameAsync(dbContext, gameId);
        if (game is null) return Fail(GameResultStatus.GameNotFound);

        // A null caller is the admin console, which reads across users by design.
        if (userId is not null && !await IsParticipantAsync(dbContext, gameId, userId))
        {
            return Fail(GameResultStatus.NotParticipant);
        }

        return await BuildStateAsync(gameId);
    }

    // ── Field management ──

    public async Task<GameResult<GameStateResponse>> JoinGameAsync(JoinGameRequest request, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var code = request.JoinCode.Trim().ToUpperInvariant();

        // The unique index only covers live games, so the lookup must filter on state too —
        // a bare code match could otherwise hit a game from last season.
        var game = await dbContext.Games.FirstOrDefaultAsync(g =>
            g.JoinCode == code && !g.IsDeleted && g.State < GameState.Completed);

        if (game is null) return Fail(GameResultStatus.JoinCodeInvalid);

        var alreadyIn = await dbContext.GameParticipants
            .AnyAsync(p => p.GameId == game.GameId && p.UserId == userId && !p.IsDeleted);

        if (alreadyIn) return Fail(GameResultStatus.AlreadyJoined);

        var holeNumbers = GameScoreReader.HoleNumbersFor(game);

        var teeboxCheck = await ValidateTeeboxAsync(dbContext, request.TeeboxId, game.CourseId, holeNumbers, allowArchived: false);
        if (teeboxCheck is not null) return teeboxCheck;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var participant = new GameParticipant
        {
            GameId = game.GameId,
            UserId = userId,
            DisplayName = await ResolveDisplayNameAsync(dbContext, userId),
            TeeboxId = request.TeeboxId,
            CourseHandicap = request.CourseHandicap,
            Team = request.Team,
            CreatedBy = userId,
            CreatedOn = today,
            UpdatedBy = userId,
            UpdatedOn = today
        };

        dbContext.GameParticipants.Add(participant);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Two devices joined at once. The other one won; that is the same outcome.
            _logger.LogInformation(ex, "Concurrent join of game {GameId} by {UserId}", game.GameId, userId);
            return Fail(GameResultStatus.AlreadyJoined);
        }

        if (request.RoundId is { } roundId)
        {
            var link = await ApplyRoundLinkAsync(dbContext, game, participant, roundId, userId, today);
            if (link is not null) return link;

            await dbContext.SaveChangesAsync();
        }

        return await BuildStateAsync(game.GameId);
    }

    public async Task<GameResult<GameStateResponse>> AddParticipantAsync(
        long gameId, AddParticipantRequest request, string hostUserId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var game = await LoadGameAsync(dbContext, gameId);
        if (game is null) return Fail(GameResultStatus.GameNotFound);
        if (game.HostUserId != hostUserId) return Fail(GameResultStatus.NotHost);
        if (game.State != GameState.Setup) return Fail(GameResultStatus.GameNotInSetup);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        string displayName;

        if (request.UserId is { } addedUserId)
        {
            // Without this a host could push a game into any stranger's list by user id.
            if (addedUserId != hostUserId && !await _friendService.AreFriendsAsync(hostUserId, addedUserId))
            {
                return Fail(GameResultStatus.NotFriends, "You can only add friends to a game. Add them as a guest instead.");
            }

            var alreadyIn = await dbContext.GameParticipants
                .AnyAsync(p => p.GameId == gameId && p.UserId == addedUserId && !p.IsDeleted);

            if (alreadyIn) return Fail(GameResultStatus.AlreadyJoined);

            displayName = await ResolveDisplayNameAsync(dbContext, addedUserId);
        }
        else
        {
            displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Guest" : request.DisplayName.Trim();
        }

        var holeNumbers = GameScoreReader.HoleNumbersFor(game);

        var teeboxCheck = await ValidateTeeboxAsync(dbContext, request.TeeboxId, game.CourseId, holeNumbers, allowArchived: false);
        if (teeboxCheck is not null) return teeboxCheck;

        dbContext.GameParticipants.Add(new GameParticipant
        {
            GameId = gameId,
            UserId = request.UserId,
            DisplayName = displayName,
            TeeboxId = request.TeeboxId,
            CourseHandicap = request.CourseHandicap,
            Team = request.Team,
            CreatedBy = hostUserId,
            CreatedOn = today,
            UpdatedBy = hostUserId,
            UpdatedOn = today
        });

        await dbContext.SaveChangesAsync();

        return await BuildStateAsync(gameId);
    }

    public async Task<GameResult<GameStateResponse>> UpdateParticipantAsync(
        long gameId, long participantId, UpdateParticipantRequest request, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var game = await LoadGameAsync(dbContext, gameId);
        if (game is null) return Fail(GameResultStatus.GameNotFound);

        var participant = await LoadParticipantAsync(dbContext, gameId, participantId);
        if (participant is null) return Fail(GameResultStatus.ParticipantNotFound);

        // The host manages the field; a player manages their own line.
        if (game.HostUserId != userId && participant.UserId != userId)
        {
            return Fail(GameResultStatus.NotHost);
        }

        if (request.TeeboxId is { } teeboxId && teeboxId != participant.TeeboxId)
        {
            if (participant.RoundId is not null)
            {
                return Fail(GameResultStatus.ParticipantUsesLinkedRound,
                    "Tees come from the linked round. Unlink it first to choose different tees.");
            }

            var holeNumbers = GameScoreReader.HoleNumbersFor(game);
            var teeboxCheck = await ValidateTeeboxAsync(dbContext, teeboxId, game.CourseId, holeNumbers, allowArchived: false);
            if (teeboxCheck is not null) return teeboxCheck;

            participant.TeeboxId = teeboxId;
        }

        if (request.CourseHandicap is { } handicap) participant.CourseHandicap = handicap;
        if (request.Team is { } team) participant.Team = team;

        if (!string.IsNullOrWhiteSpace(request.DisplayName) && participant.UserId is null)
        {
            // Only a guest's name is free text; a registered golfer's is snapshotted from their profile.
            participant.DisplayName = request.DisplayName.Trim();
        }

        participant.UpdatedBy = userId;
        participant.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);

        await dbContext.SaveChangesAsync();

        return await BuildStateAsync(gameId);
    }

    public async Task<GameResult<GameStateResponse>> RemoveParticipantAsync(
        long gameId, long participantId, string hostUserId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var game = await LoadGameAsync(dbContext, gameId);
        if (game is null) return Fail(GameResultStatus.GameNotFound);
        if (game.HostUserId != hostUserId) return Fail(GameResultStatus.NotHost);

        // Once a game is running, removing a side would rewrite a bet in flight. Abandon it instead.
        if (game.State != GameState.Setup) return Fail(GameResultStatus.GameNotInSetup);

        var participant = await LoadParticipantAsync(dbContext, gameId, participantId);
        if (participant is null) return Fail(GameResultStatus.ParticipantNotFound);

        participant.IsDeleted = true;
        participant.UpdatedBy = hostUserId;
        participant.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);

        await dbContext.SaveChangesAsync();

        return await BuildStateAsync(gameId);
    }

    public async Task<GameResult<GameStateResponse>> LinkRoundAsync(long gameId, long? roundId, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var game = await LoadGameAsync(dbContext, gameId);
        if (game is null) return Fail(GameResultStatus.GameNotFound);

        var participant = await dbContext.GameParticipants
            .FirstOrDefaultAsync(p => p.GameId == gameId && p.UserId == userId && !p.IsDeleted);

        if (participant is null) return Fail(GameResultStatus.NotParticipant);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (roundId is null)
        {
            // Unlink. The teebox stays put; host-entered strokes become the source again.
            participant.RoundId = null;
            participant.UpdatedBy = userId;
            participant.UpdatedOn = today;

            await dbContext.SaveChangesAsync();
            return await BuildStateAsync(gameId);
        }

        var link = await ApplyRoundLinkAsync(dbContext, game, participant, roundId.Value, userId, today);
        if (link is not null) return link;

        await dbContext.SaveChangesAsync();

        return await BuildStateAsync(gameId);
    }

    // ── Host-entered strokes ──

    public async Task<GameResult<GameStateResponse>> UpsertParticipantHoleAsync(
        long gameId, long participantId, int holeNumber, UpsertGameHoleRequest request, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var guard = await LoadForHoleWriteAsync(dbContext, gameId, participantId, holeNumber, userId);
        if (guard.Failure is not null) return guard.Failure;

        var participant = guard.Participant!;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var score = await dbContext.GameHoleScores.FirstOrDefaultAsync(s =>
            s.GameParticipantId == participantId && s.HoleNumber == holeNumber && !s.IsDeleted);

        if (score is null)
        {
            score = new GameHoleScore
            {
                GameParticipantId = participantId,
                HoleNumber = holeNumber,
                Strokes = request.Strokes,
                CreatedBy = userId,
                CreatedOn = today,
                UpdatedBy = userId,
                UpdatedOn = today
            };

            dbContext.GameHoleScores.Add(score);
        }
        else
        {
            score.Strokes = request.Strokes;
            score.UpdatedBy = userId;
            score.UpdatedOn = today;
        }

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Two devices wrote this hole for the first time at once. The row the other one
            // inserted is the truth to update; re-read and apply on top of it.
            _logger.LogInformation(ex,
                "Concurrent first write of hole {HoleNumber} for participant {ParticipantId}; retrying as an update",
                holeNumber, participantId);

            dbContext.Entry(score).State = EntityState.Detached;

            score = await dbContext.GameHoleScores.FirstOrDefaultAsync(s =>
                s.GameParticipantId == participantId && s.HoleNumber == holeNumber && !s.IsDeleted);

            if (score is null) throw;

            score.Strokes = request.Strokes;
            score.UpdatedBy = userId;
            score.UpdatedOn = today;

            await dbContext.SaveChangesAsync();
        }

        FairwayFinderDiagnostics.GameHolesPosted.Add(1);

        return await BuildStateAsync(gameId);
    }

    public async Task<GameResult<GameStateResponse>> ClearParticipantHoleAsync(
        long gameId, long participantId, int holeNumber, string userId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var guard = await LoadForHoleWriteAsync(dbContext, gameId, participantId, holeNumber, userId);
        if (guard.Failure is not null) return guard.Failure;

        var score = await dbContext.GameHoleScores.FirstOrDefaultAsync(s =>
            s.GameParticipantId == participantId && s.HoleNumber == holeNumber && !s.IsDeleted);

        if (score is not null)
        {
            score.IsDeleted = true;
            score.UpdatedBy = userId;
            score.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);

            await dbContext.SaveChangesAsync();
        }

        return await BuildStateAsync(gameId);
    }

    // ── Lifecycle ──

    public async Task<GameResult<GameStateResponse>> StartGameAsync(long gameId, string hostUserId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var game = await LoadGameAsync(dbContext, gameId);
        if (game is null) return Fail(GameResultStatus.GameNotFound);
        if (game.HostUserId != hostUserId) return Fail(GameResultStatus.NotHost);
        if (game.State != GameState.Setup) return Fail(GameResultStatus.GameNotInSetup);

        var participants = await LoadParticipantsAsync(dbContext, gameId);

        var fieldCheck = ValidateField(game.GameType, participants);
        if (fieldCheck is not null) return fieldCheck;

        game.State = GameState.Active;
        game.UpdatedBy = hostUserId;
        game.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);

        await dbContext.SaveChangesAsync();

        FairwayFinderDiagnostics.GamesStarted.Add(1, new TagList
        {
            { FairwayFinderDiagnostics.Tags.GameType, game.GameType.ToString() },
            { FairwayFinderDiagnostics.Tags.GameHasGuests, participants.Any(p => p.UserId is null) }
        });

        var state = await BuildStateAsync(gameId);

        await GameNotifications.NotifyParticipantsOfGameStartAsync(
            dbContext, _pushService, _logger, game, participants);

        return state;
    }

    public async Task<GameResult<GameStateResponse>> CompleteGameAsync(long gameId, string hostUserId)
    {
        using var activity = FairwayFinderDiagnostics.GamesActivity
            .StartActivity(FairwayFinderDiagnostics.ActivityNames.GameComplete);
        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.GameId, gameId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var game = await LoadGameAsync(dbContext, gameId);
        if (game is null) return Fail(GameResultStatus.GameNotFound);
        if (game.HostUserId != hostUserId) return Fail(GameResultStatus.NotHost);
        if (game.State == GameState.Completed) return Fail(GameResultStatus.GameAlreadyComplete);
        if (game.State != GameState.Active) return Fail(GameResultStatus.GameNotActive);

        var participants = await LoadParticipantsAsync(dbContext, gameId);
        var read = await _reader.ReadAsync(game, participants);
        var board = _engineResolver.For(game.GameType).Score(read.Context);

        // Serialized as the base type on purpose: with the concrete type the polymorphic
        // discriminator is omitted and the snapshot cannot be read back.
        game.FinalScoreboard = JsonSerializer.Serialize<GameScoreboard>(board);
        game.State = GameState.Completed;
        game.UpdatedBy = hostUserId;
        game.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);

        await dbContext.SaveChangesAsync();

        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.GameParticipants, participants.Count);

        FairwayFinderDiagnostics.GamesCompleted.Add(1, new TagList
        {
            { FairwayFinderDiagnostics.Tags.GameType, game.GameType.ToString() }
        });

        var state = await BuildStateAsync(gameId);

        await GameNotifications.NotifyParticipantsOfGameResultAsync(
            dbContext, _pushService, _logger, game, participants, board.Summary);

        return state;
    }

    public async Task<GameResult<GameStateResponse>> AbandonGameAsync(long gameId, string hostUserId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var game = await LoadGameAsync(dbContext, gameId);
        if (game is null) return Fail(GameResultStatus.GameNotFound);
        if (game.HostUserId != hostUserId) return Fail(GameResultStatus.NotHost);
        if (game.State == GameState.Completed) return Fail(GameResultStatus.GameAlreadyComplete);

        game.State = GameState.Abandoned;
        game.UpdatedBy = hostUserId;
        game.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);

        await dbContext.SaveChangesAsync();

        return await BuildStateAsync(gameId);
    }

    // ── Building the response ──

    /// <summary>
    /// Reads the game back and scores it. Every mutating call ends here, so the app always gets
    /// the current scoreboard without a follow-up GET.
    /// </summary>
    private async Task<GameResult<GameStateResponse>> BuildStateAsync(long gameId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var game = await dbContext.Games.AsNoTracking()
            .FirstOrDefaultAsync(g => g.GameId == gameId && !g.IsDeleted);

        if (game is null) return Fail(GameResultStatus.GameNotFound);

        var courseName = await dbContext.Courses.AsNoTracking()
            .Where(c => c.CourseId == game.CourseId)
            .Select(c => c.CourseName)
            .FirstOrDefaultAsync() ?? "";

        using var activity = FairwayFinderDiagnostics.GamesActivity
            .StartActivity(FairwayFinderDiagnostics.ActivityNames.GameScore);
        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.GameId, gameId);
        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.GameType, game.GameType.ToString());

        var participants = await LoadParticipantsAsync(dbContext, gameId);
        activity?.SetTag(FairwayFinderDiagnostics.ActivityTags.GameParticipants, participants.Count);

        var read = await _reader.ReadAsync(game, participants);

        var publicIds = await dbContext.UserProfiles.AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Select(p => new { p.UserId, p.PublicIdentifier })
            .ToListAsync();

        var publicIdMap = publicIds
            .Where(p => p.UserId is not null)
            .ToDictionary(p => p.UserId!, p => p.PublicIdentifier);

        var response = new GameStateResponse
        {
            GameId = game.GameId,
            GameType = game.GameType,
            State = game.State,
            CourseId = game.CourseId,
            CourseName = courseName,
            DatePlayed = game.DatePlayed,
            HostUserId = game.HostUserId,
            JoinCode = game.JoinCode,
            Rules = read.Context.Rules,
            HoleNumbers = [.. read.HoleNumbers],
            Participants =
            [
                .. participants.Select(p => new GameParticipantResponse
                {
                    ParticipantId = p.GameParticipantId,
                    UserId = p.UserId,
                    PublicIdentifier = p.UserId is not null && publicIdMap.TryGetValue(p.UserId, out var pid) ? pid : null,
                    DisplayName = p.DisplayName,
                    IsGuest = p.UserId is null,
                    IsHost = p.UserId == game.HostUserId,
                    RoundId = p.RoundId,
                    TeeboxId = p.TeeboxId,
                    TeeboxName = read.TeeboxNames.TryGetValue(p.GameParticipantId, out var tee) ? tee : "",
                    CourseHandicap = p.CourseHandicap,
                    PlayingHandicap = read.PlayingHandicaps[p.GameParticipantId],
                    Team = p.Team,
                    HolesEntered = read.HolesEntered[p.GameParticipantId],
                    RoundUnavailable = read.RoundUnavailable[p.GameParticipantId]
                })
            ],
            Scoreboard = ResolveScoreboard(game, read)
        };

        return GameResult<GameStateResponse>.Ok(response);
    }

    /// <summary>
    /// The board the caller should see. A game in setup has none — the field may not even be valid
    /// for the game type yet. A completed game serves its snapshot, which is the entire reason the
    /// column exists: rounds stay editable, and a settled bet must not change months later.
    /// </summary>
    private GameScoreboard? ResolveScoreboard(Game game, GameScoreReader.GameReadModel read)
    {
        if (game.State == GameState.Setup) return null;

        if (game.State == GameState.Completed && !string.IsNullOrWhiteSpace(game.FinalScoreboard))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<GameScoreboard>(game.FinalScoreboard);
                if (snapshot is not null) return snapshot;
            }
            catch (JsonException ex)
            {
                // Schema drift on an old snapshot. A recompute is a worse answer than the frozen
                // one, but it beats failing the whole read.
                _logger.LogError(ex,
                    "Could not read the stored scoreboard for game {GameId}; recomputing", game.GameId);
            }
        }

        return _engineResolver.For(game.GameType).Score(read.Context);
    }

    // ── Guards and helpers ──

    /// <summary>
    /// The shared guard for writing or clearing a host-entered hole: the game must be live, the
    /// caller must be entitled, the hole must be in the game, and the participant must not be
    /// scored from a linked round.
    /// </summary>
    private static async Task<(GameResult<GameStateResponse>? Failure, GameParticipant? Participant)>
        LoadForHoleWriteAsync(
            ApplicationDbContext dbContext, long gameId, long participantId, int holeNumber, string userId)
    {
        var game = await LoadGameAsync(dbContext, gameId);
        if (game is null) return (Fail(GameResultStatus.GameNotFound), null);

        var participant = await LoadParticipantAsync(dbContext, gameId, participantId);
        if (participant is null) return (Fail(GameResultStatus.ParticipantNotFound), null);

        // The host scores guests; a player may also fix their own line.
        if (game.HostUserId != userId && participant.UserId != userId)
        {
            return (Fail(GameResultStatus.NotHost), null);
        }

        if (game.State == GameState.Completed) return (Fail(GameResultStatus.GameAlreadyComplete), null);
        if (game.State == GameState.Abandoned) return (Fail(GameResultStatus.GameNotActive), null);

        if (participant.RoundId is not null)
        {
            // Without this the write succeeds, returns 200, and is never read — the linked round
            // is the source of truth for this participant.
            return (Fail(GameResultStatus.ParticipantUsesLinkedRound,
                "This player's strokes come from their linked round. Edit the round instead."), null);
        }

        if (!GameScoreReader.HoleNumbersFor(game).Contains(holeNumber))
        {
            return (Fail(GameResultStatus.HoleNotInGame), null);
        }

        return (null, participant);
    }

    /// <summary>
    /// Points a participant at a round of their own. The teebox is taken from the round rather
    /// than trusted from the caller: the round's scores join to holes on that teebox, so anything
    /// else would read par and stroke index off a different hole set.
    /// </summary>
    private static async Task<GameResult<GameStateResponse>?> ApplyRoundLinkAsync(
        ApplicationDbContext dbContext,
        Game game,
        GameParticipant participant,
        long roundId,
        string userId,
        DateOnly today)
    {
        var round = await dbContext.Rounds.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoundId == roundId && !r.IsDeleted);

        if (round is null || round.UserId != userId) return Fail(GameResultStatus.RoundNotOwned);
        if (round.CourseId != game.CourseId) return Fail(GameResultStatus.RoundNotOnGameCourse);

        var holeNumbers = GameScoreReader.HoleNumbersFor(game);

        // An archived teebox is fine here: an admin may have versioned the course mid-round, and
        // the round is legitimately on the old tee. The guard only applies when a tee is chosen.
        var teeboxCheck = await ValidateTeeboxAsync(dbContext, round.TeeboxId, game.CourseId, holeNumbers, allowArchived: true);
        if (teeboxCheck is not null) return teeboxCheck;

        participant.RoundId = roundId;
        participant.TeeboxId = round.TeeboxId;
        participant.UpdatedBy = userId;
        participant.UpdatedOn = today;

        return null;
    }

    /// <summary>
    /// A teebox is usable for a game when it belongs to the game's course, covers every hole the
    /// game plays, and (unless inherited from a linked round) has not been superseded.
    /// </summary>
    private static async Task<GameResult<GameStateResponse>?> ValidateTeeboxAsync(
        ApplicationDbContext dbContext, long teeboxId, long courseId, IReadOnlyList<int> holeNumbers, bool allowArchived)
    {
        var teebox = await dbContext.Teeboxes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TeeboxId == teeboxId && !t.IsDeleted);

        if (teebox is null) return Fail(GameResultStatus.TeeboxNotOnCourse, "That teebox does not exist.");
        if (teebox.CourseId != courseId) return Fail(GameResultStatus.TeeboxNotOnCourse, "That teebox is on a different course.");

        if (!allowArchived && teebox.ArchivedOn is not null)
        {
            return Fail(GameResultStatus.TeeboxArchived, "That teebox has been replaced by a newer version.");
        }

        var covered = await dbContext.Holes.AsNoTracking()
            .Where(h => h.TeeboxId == teeboxId && !h.IsDeleted)
            .Select(h => h.HoleNumber)
            .ToListAsync();

        var missing = holeNumbers.Except(covered).OrderBy(n => n).ToList();

        if (missing.Count > 0)
        {
            return Fail(GameResultStatus.TeeboxShapeMismatch,
                $"Those tees have no hole {string.Join(", ", missing)}, which this game plays.");
        }

        return null;
    }

    /// <summary>
    /// Whether a field suits the game type. Checked at start rather than at create, so a host can
    /// build the field in any order.
    /// </summary>
    private static GameResult<GameStateResponse>? ValidateField(
        GameType gameType, IReadOnlyList<GameParticipant> participants)
    {
        if (participants.Count < 2)
        {
            return Fail(GameResultStatus.ParticipantCountInvalid, "A game needs at least two players.");
        }

        var teamed = participants.Count(p => p.Team is not null);
        if (teamed != 0 && teamed != participants.Count)
        {
            return Fail(GameResultStatus.ParticipantCountInvalid,
                "Either everyone is on a team or nobody is.");
        }

        switch (gameType)
        {
            case GameType.MatchPlay:
                var sides = teamed == 0
                    ? participants.Count
                    : participants.Select(p => p.Team!.Value).Distinct().Count();

                if (sides != 2)
                {
                    return Fail(GameResultStatus.ParticipantCountInvalid,
                        $"Match play needs exactly two sides; this game has {sides}.");
                }

                break;

            case GameType.Skins:
                if (teamed != 0)
                {
                    return Fail(GameResultStatus.ParticipantCountInvalid,
                        "Skins is an individual game — remove the teams.");
                }

                break;
        }

        return null;
    }

    private static IReadOnlyList<int> ShapeHoleNumbers(bool fullRound, bool frontNine, bool backNine)
    {
        if (fullRound) return [.. Enumerable.Range(1, 18)];
        if (frontNine) return [.. Enumerable.Range(1, 9)];
        if (backNine) return [.. Enumerable.Range(10, 9)];

        return [.. Enumerable.Range(1, 18)];
    }

    private static Task<Game?> LoadGameAsync(ApplicationDbContext dbContext, long gameId)
        => dbContext.Games.FirstOrDefaultAsync(g => g.GameId == gameId && !g.IsDeleted);

    private static Task<GameParticipant?> LoadParticipantAsync(
        ApplicationDbContext dbContext, long gameId, long participantId)
        => dbContext.GameParticipants.FirstOrDefaultAsync(p =>
            p.GameParticipantId == participantId && p.GameId == gameId && !p.IsDeleted);

    private static async Task<List<GameParticipant>> LoadParticipantsAsync(
        ApplicationDbContext dbContext, long gameId)
        => await dbContext.GameParticipants.AsNoTracking()
            .Where(p => p.GameId == gameId && !p.IsDeleted)
            .OrderBy(p => p.GameParticipantId)
            .ToListAsync();

    private static Task<bool> IsParticipantAsync(ApplicationDbContext dbContext, long gameId, string userId)
        => dbContext.GameParticipants.AnyAsync(p => p.GameId == gameId && p.UserId == userId && !p.IsDeleted);

    private static async Task<string> ResolveDisplayNameAsync(ApplicationDbContext dbContext, string userId)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        var name = DisplayNameHelper.Build(user);

        return string.IsNullOrWhiteSpace(name) ? "Guest" : name;
    }

    /// <summary>
    /// Saves the game with a fresh join code, retrying on the collision the partial unique index
    /// raises. Random six-character codes over a 32-letter alphabet collide rarely, but "rarely"
    /// is not "never" once a season's games are live.
    /// </summary>
    private async Task SaveWithFreshJoinCodeAsync(ApplicationDbContext dbContext, Game game)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; ; attempt++)
        {
            game.JoinCode = NewJoinCode();
            dbContext.Games.Add(game);

            try
            {
                await dbContext.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt < maxAttempts)
            {
                _logger.LogInformation(ex,
                    "Join code {JoinCode} collided with a live game; retrying (attempt {Attempt})",
                    game.JoinCode, attempt);

                dbContext.Entry(game).State = EntityState.Detached;
            }
        }
    }

    private static string NewJoinCode()
    {
        var code = new char[JoinCodeLength];

        for (var i = 0; i < JoinCodeLength; i++)
        {
            code[i] = JoinCodeAlphabet[RandomNumberGenerator.GetInt32(JoinCodeAlphabet.Length)];
        }

        return new string(code);
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
    /// True when a save failed because it collided with a unique index. Reflection rather than a
    /// Npgsql reference, so Features stays provider-agnostic.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.GetType().Name == "PostgresException"
           && ex.InnerException.GetType().GetProperty("SqlState")?.GetValue(ex.InnerException) as string == "23505";

    private static GameResult<GameStateResponse> Fail(GameResultStatus status, string? detail = null)
        => GameResult<GameStateResponse>.Fail(status, detail);
}
