# Product analytics and error tracking

The server sends named product events (what golfers did) and unhandled exceptions to PostHog. Both are switched on by one setting, `PostHog:ProjectToken`; without it, nothing leaves the process. Logs and traces also reach PostHog in production, but over OTLP, which is covered in [logging-and-observability.md](logging-and-observability.md).

## Wiring

`RegisterFeatureServices()` in `src/FairwayFinder.Features/ServiceRegistration.cs` decides at startup:

| `PostHog:ProjectToken` | `IProductAnalytics` | Exception sink |
|---|---|---|
| set | `PostHogProductAnalytics`, plus `AddPostHog()` (the PostHog .NET SDK client) | `PostHogExceptionSink` registered as an `ILogEventSink` |
| empty (dev, tests) | `NullProductAnalytics.Instance` (no-op) | none |

The whole `PostHog` config section is bound straight onto the SDK's `PostHogOptions`, so any SDK option works there, `PostHog:HostUrl` included. Both `appsettings.json` files ship with an empty token and `HostUrl: https://us.i.posthog.com`; production sets the token through the environment (`PostHog__ProjectToken`).

All of this lives under `src/FairwayFinder.Features/Services/Analytics/`.

## `IProductAnalytics`

```csharp
void Capture(string userId, string eventName,
             IReadOnlyDictionary<string, object?>? properties = null,
             IReadOnlyDictionary<string, object?>? personProperties = null);
```

- **Fire-and-forget.** The SDK queues events and flushes them in batches on its own timer, and once more when the container disposes the client at shutdown. A slow or unreachable PostHog never holds up a request.
- **Never fails the caller.** `PostHogProductAnalytics.Capture` wraps the SDK call in `try/catch` and logs a Warning (`Could not queue analytics event {EventName}`) instead of throwing.
- **`properties`** are facts about this occurrence. Null values are dropped. Every event also gets `app` set to the host's application name, so an event raised by the admin host could never be mistaken for something done in the app (today only the API emits events).
- **`personProperties`** are sent as `$set` and stored on the PostHog person. Only the auth endpoints use this, to set `email` and `name` (`AuthEndpoints.PersonProperties`), refreshed on every login so a name change catches up.

## Distinct id rules

- The distinct id is **the golfer's Identity user id** (`AspNetUsers.Id`, the JWT `NameIdentifier` claim). The iOS app passes the same id to PostHog's `identify`, which is what joins server-side and app-side events into one person.
- Never an email. Email is a person property, not an id.
- Never the acting admin's id on a golfer's behalf. Admin paths do not emit golfer events at all.
- For events that involve two golfers, the id is the one who acted: the sender of a friend request, the viewer of a friend's stats, the host who started a game.

## Events

Every event name is a constant in `AnalyticsEvents` (`AnalyticsEvents.cs`): snake_case, past tense. Property names are in `AnalyticsEvents.Props`, and the enum-like values in `StatsScopes`, `FriendViews` and `EntryModes`. Never use an inline string; a typo splits one event into two in PostHog.

| Event | Emitted from | Trigger (API route) | Properties |
|---|---|---|---|
| `app_active` | `ActiveGolferTracker.MarkActive`, called by `ActiveGolferMiddleware` and the login/register endpoints | First authenticated request of the UTC day, or a login/register | none |
| `user_signed_up` | `AuthEndpoints` | `POST /api/auth/register` | person: `email`, `name` |
| `user_logged_in` | `AuthEndpoints` | `POST /api/auth/login` | person: `email`, `name` |
| `push_device_registered` | `PushNotificationService.RegisterDeviceAsync` | `POST /api/devices`, only for a token not seen before (the app re-registers on every launch) | none |
| `round_started` | `RoundEntryService.StartRoundAsync` | `POST /api/rounds/start` | `round_id`, `course_id`, `teebox_id`, `shot_tracking`, `hole_stats` |
| `round_completed` | `RoundEntryService.CompleteRoundAsync` | `POST /api/rounds/{id}/complete` | `round_id`, `course_id`, `holes`, `score`, `to_par`, `shot_tracking`, `hole_stats`, `entry = hole_by_hole` |
| `round_completed` | `RoundService.CreateRoundAsync` | `POST /api/rounds` (deprecated whole-card submit) | same, with `entry = full_card` |
| `round_edited` | `RoundEndpoints` | `PUT /api/rounds/{id}` | `round_id` |
| `round_deleted` | `RoundEndpoints` | `DELETE /api/rounds/{id}` | `round_id` |
| `stats_viewed` | `StatsEndpoints.CaptureStatsViewed` | `GET /api/stats`, `/api/stats/courses/{id}`, `/api/stats/courses/{id}/holes` | `scope` (`overall`, `course`, `course_holes`), `baseline_level`, `filtered`, `course_id` |
| `game_created` | `GameService.CreateGameAsync` | `POST /api/games` | `game_id`, `game_type` |
| `game_joined` | `GameService.JoinGameAsync` | `POST /api/games/join` | `game_id`, `game_type` |
| `game_started` | `GameService.StartGameAsync` | `POST /api/games/{id}/start` | `game_id`, `game_type`, `players` |
| `game_completed` | `GameService.CompleteGameAsync` | `POST /api/games/{id}/complete` | `game_id`, `game_type`, `players` |
| `game_abandoned` | `GameService.AbandonGameAsync` | `POST /api/games/{id}/abandon` | `game_id`, `game_type` |
| `friend_request_sent` | `FriendService.SendRequestAsync` | `POST /api/friends/requests` (new, or reactivating a rejected/cancelled one) | none |
| `friend_request_accepted` | `FriendService.AcceptRequestAsync` | `PUT /api/friends/requests/{id}/accept` | `auto_accepted = false` |
| `friend_request_accepted` | `FriendService.SendRequestAsync` | `POST /api/friends/requests` when the other golfer had already sent one, so it is accepted in place | `auto_accepted = true` |
| `friend_activity_viewed` | `FriendEndpoints.CaptureFriendActivityViewed` | `GET /api/friends/{publicId}/rounds`, `/rounds/{roundId}`, `/stats` | `view` (`rounds`, `round`, `stats`) |

`round_completed` is one event for both entry paths, told apart by `entry`, so every posted round counts once however it arrived. See [../features/rounds.md](../features/rounds.md) for the two paths.

## Where to capture: service or endpoint

The rule (also in `CLAUDE.md`):

- **In the Features service**, next to the matching `FairwayFinderDiagnostics` counter, when only golfer paths call the method. `RoundEntryService`, `GameService`, `FriendService.SendRequestAsync`/`AcceptRequestAsync`, `PushNotificationService.RegisterDeviceAsync` and `RoundService.CreateRoundAsync` are all golfer-only.
- **In the API endpoint** when the admin console, or another golfer's view, also calls the method. There, only the endpoint knows that the signed-in golfer is the one acting:
  - `RoundService.UpdateRoundAsync` and `DeleteRoundAsync` are also how the admin console repairs rounds, so `round_edited` and `round_deleted` are captured in `RoundEndpoints`.
  - `StatsService` serves the admin stats diagnostics too, so `stats_viewed` is captured in `StatsEndpoints`.
  - Friend views reuse `RoundService`/`StatsService` with the friend's id as the target, so `friend_activity_viewed` is captured in `FriendEndpoints` under the viewer's id.
  - Login and register are endpoint-only concerns anyway.

To add an event: add the constant (and any new property names) to `AnalyticsEvents`, inject `IProductAnalytics` where the rule above says, capture after the action has succeeded, and assert it in the matching API integration test (see [Testing](#testing)).

## `app_active` and active golfers

`app_active` is the event to use for daily/weekly active golfers and overall retention. It is sent at most once per golfer per UTC day.

- `ActiveGolferTracker` is a singleton (registered in `src/FairwayFinder.Api/Program.cs`, API only) holding a `ConcurrentDictionary<userId, DateOnly>` of the last day each golfer was seen. Almost every call returns at the first dictionary lookup. When two requests open a day together, `AddOrUpdate` makes sure only the one that moved the date forward sends.
- `UseActiveGolferTracking()` (`src/FairwayFinder.Api/Middleware/ActiveGolferMiddleware.cs`) runs after `UseAuthentication()` and calls `MarkActive` for any request with a `NameIdentifier` claim. It reads the claim directly rather than through `GetUserId()`, which throws when the claim is missing.
- It hooks requests rather than token refresh because the app only refreshes when its access token has expired: a golfer who opened the app either side of midnight would otherwise not count on the second day.
- Login and register call `MarkActive` themselves, since those requests are anonymous when they reach the middleware.
- The memory is per process, so a restart or a second instance can send a second event for the same day. That is harmless: active-golfer charts count unique people, not events.
- It takes a `TimeProvider`, which is how `tests/FairwayFinder.Features.Tests/ActiveGolferTrackerTests.cs` moves across midnight.

## Error tracking

`PostHogExceptionSink` sends exceptions to PostHog Error Tracking. It is a Serilog `ILogEventSink` in DI, like `DatabaseLogSink`, so one sink covers API requests, admin Blazor circuits and background jobs.

- **What is captured** (`ShouldCapture`): Error or Fatal events that carry an exception, except those whose `SourceContext` starts with `PostHog` (the SDK's own failures would feed themselves). Because `GlobalExceptionHandler` logs 4xx responses at Information, client mistakes never reach Error Tracking.
- **Distinct id**: the event's `UserId` property, which `UseUserLogContext()` or `LogContextCircuitHandler` pushed (see [logging-and-observability.md](logging-and-observability.md#who-caused-it-user-context-enrichment)). On the admin host that is the admin's id, which is correct here: the admin is the one who hit the error. With no user (background jobs, anonymous requests) it falls back to `system:<ApplicationName>` and sets `$process_person_profile = false`, so that shared id never becomes a PostHog person collecting every anonymous error.
- **Properties**: `log_level`, `log_message_template` (the template, not the rendered text, so occurrences group), and when present `SourceContext`, `Application`, `RouteTemplate`, `RequestPath`, `RequestMethod`, `trace_id`.
- **Lazy client**: the sink takes `Lazy<IPostHogClient>`. Serilog builds the logger, and this sink with it, while the container is still resolving `ILoggerFactory`, and the PostHog client needs that factory. Resolving eagerly would be a circular dependency.
- **Never throws back**: `CaptureException` is wrapped in `try/catch`, and a `[ThreadStatic]` flag stops anything the SDK logs during the call from re-entering the sink.

## Testing

- In integration tests the host's `IProductAnalytics` is replaced by `RecordingProductAnalytics` (`tests/FairwayFinder.IntegrationTests.Common/Fakes/`), cleared before every test. Assert with `Analytics.For(userId, AnalyticsEvents.X)` and check properties on the returned `CapturedEvent`s. Examples: `ActiveGolferTests`, `RoundEntryTests`, `GameEndpointsTests`, `FriendEndpointsTests`, `StatsEndpointsTests`, `AuthEndpointsTests`, `DeviceEndpointsTests`.
- Unit tests that construct services by hand pass `NullProductAnalytics.Instance`.
- `tests/FairwayFinder.Features.Tests/PostHogExceptionSinkTests.cs` covers the capture filter, properties, the anonymous person flag and the never-throw guarantee using the sink's internal constructor.

More in [../testing.md](../testing.md).
