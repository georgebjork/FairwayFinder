# API endpoint reference

Every route the `FairwayFinder.Api` host maps, grouped by the file that maps it under `src/FairwayFinder.Api/`. For auth, error format, validation and the pipeline, read [overview.md](overview.md) first.

Conventions used below:

- **Auth**: `User` means any signed-in golfer (a valid JWT); `Admin` means the `Admin` role; `Anon` means `.AllowAnonymous()`.
- **Validated** means a `ValidationFilter<T>` runs first; a failure is `400` `ValidationProblemDetails`.
- Every `User` route also returns `401` without a valid token. That is not repeated per row.
- `404`/`403`/`409` bodies are ProblemDetails unless stated (see [overview.md](overview.md#errors-and-problemdetails)).
- `level` is an optional `BaselineLevel` query value (`Tour`, `Scratch`, `Hcp5`, `Hcp10`, `Hcp15`, `Hcp20`, `Hcp25`). When omitted, the golfer's saved `SgBaselineLevel` from their profile is used (`ResolveLevelAsync`). It only affects strokes-gained figures; see [../features/stats-and-strokes-gained.md](../features/stats-and-strokes-gained.md).

## Summary

| File | Prefix | Auth |
|---|---|---|
| `Auth/AuthEndpoints.cs` | `/api/auth` | Anon |
| `Endpoints/CourseEndpoints.cs` | `/api/courses` | User |
| `Endpoints/RoundEndpoints.cs` | `/api/rounds` | User |
| `Endpoints/StatsEndpoints.cs` | `/api/stats` | User |
| `Endpoints/LookupEndpoints.cs` | `/api/lookups` | User |
| `Endpoints/ProfileEndpoints.cs` | `/api/profile` | User |
| `Endpoints/FriendEndpoints.cs` | `/api/friends` | User |
| `Endpoints/GameEndpoints.cs` | `/api/games` | User |
| `Endpoints/DeviceEndpoints.cs` | `/api/devices` | User (`/test` Admin) |
| `Endpoints/AdminInviteEndpoints.cs` | `/api/admin/invites` | Admin |
| `Endpoints/PublicProfileEndpoints.cs` | `/api/public` | Anon (own rate limit) |
| `Program.cs` | `/.well-known/...`, `/register`, `/reset-password` | Anon |

## Auth (`Auth/AuthEndpoints.cs`)

Request/response types for login, refresh and password reset live in `Auth/AuthDtos.cs`; `RegisterRequest` is in `Features/Data/InviteDtos.cs`. Token mechanics are in [overview.md](overview.md#authentication).

`LoginResponse`: `AccessToken`, `RefreshToken`, `ExpiresAt` (UTC), `UserId`, `Email`, `FirstName?`, `LastName?`, `Username`.

| Method + route | Request | Response | Status codes | Calls |
|---|---|---|---|---|
| `POST /api/auth/login` | `LoginRequest`: `Email`, `Password`, `DeviceName?`. Validated | `LoginResponse` | 200; 400 missing or malformed email, missing password; 401 (unknown email or wrong password, empty body); 423 account locked out | `UserManager.FindByEmailAsync`, `SignInManager.CheckPasswordSignInAsync(lockoutOnFailure: true)`, `JwtTokenService.GenerateAccessToken`, `IssueRefreshTokenAsync` |
| `POST /api/auth/refresh` | `RefreshRequest`: `RefreshToken` | `LoginResponse` with a new refresh token | 200; 401 (unknown, expired, revoked, or reused token; reuse revokes all the user's tokens) | `JwtTokenService.RotateRefreshTokenAsync` |
| `POST /api/auth/logout` | `RefreshRequest` | none | 204 always | `JwtTokenService.RevokeRefreshTokenAsync` |
| `POST /api/auth/forgot-password` | `ForgotPasswordRequest`: `Email`. Validated | none | 204 always (no account enumeration) | `IPasswordResetService.SendResetLinkAsync` |
| `POST /api/auth/reset-password` | `ResetPasswordRequest`: `Email`, `Token` (Base64Url Identity token from the link), `NewPassword`. Validated | none | 204; 400 ProblemDetails with the service's error | `IPasswordResetService.ResetPasswordAsync`, then `JwtTokenService.RevokeAllForUserAsync` on success (every existing session is signed out) |
| `GET /api/auth/invites/{code}` | route `code` | `InviteValidationResult`: `Valid`, `Email?`, `Reason?` | 200 (invalid codes are 200 with `Valid = false`) | `IUserInvitationService.ValidateInviteAsync` |
| `POST /api/auth/register` | `RegisterRequest`: `Code`, `FirstName`, `LastName`, `PreferredTees` (0 or 1), `Password`, `DeviceName?`. Validated. Email comes from the invite | `LoginResponse` | 200; 400 invalid invite or Identity error (e.g. duplicate email) | `ValidateInviteAsync`, `UserManager.CreateAsync`, `AddToRoleAsync(User)`, `ClaimInviteAsync`, `IProfileService.GetOrCreateProfileAsync` (best effort), token issue |

## Courses (`Endpoints/CourseEndpoints.cs`)

| Method + route | Request | Response | Status codes | Calls |
|---|---|---|---|---|
| `GET /api/courses/search` | query `query` (required) | `CourseSearchResult[]`: `CourseId`, `CourseName`, `Address?`, `City?`, `State?`, `DisplayText` | 200; 400 if `query` missing | `ICourseService.SearchCoursesAsync` |
| `GET /api/courses/{courseId}/teeboxes` | query `usePreferredTees?` (bool). When true, filters by the golfer's `PreferredTees` | `TeeboxOption[]`: `TeeboxId`, `TeeboxName`, `Par`, `Rating`, `Slope`, `YardageTotal`, `IsNineHole`, `IsWomens`, `DisplayLabel` | 200 (empty list for an unknown course) | `ICourseService.GetTeeboxesAsync(courseId, filter)` |
| `GET /api/courses/teeboxes/{teeboxId}/holes` | none | `HoleInfo[]`: `HoleId`, `HoleNumber`, `Par`, `Yardage`, `Handicap` | 200 | `ICourseService.GetHolesAsync` |

Course data model: [../features/courses.md](../features/courses.md).

## Rounds (`Endpoints/RoundEndpoints.cs`)

Two ways to record a round exist side by side. Hole-by-hole entry (`start` → `holes/{n}` → `complete`) is current; the whole-round `POST /api/rounds` is deprecated but kept for iOS builds in the field. Background on both: [../features/rounds.md](../features/rounds.md).

`RoundResponse` (used by most reads): `RoundId`, `DatePlayed`, `Score`, `ScoreOut`, `ScoreIn`, `UsingHoleStats`, `UsingShotTracking`, `ExcludeFromStats`, `FullRound`/`FrontNine`/`BackNine`, `IsComplete`, `TrackingLevel`, `Games` (`RoundGameSummary[]`), `CourseId`, `CourseName`, `Teebox` (`RoundTeebox`), `Stats` (scoring distribution), `Holes` (`RoundHole[]` with `Score?`, `Stats?`, `Shots?`), `StrokesGained?`, `HoleByHoleSg?`, plus computed totals (`ParOut`/`ParIn`, `ScoreToPar`, fairways, greens, putts, penalties, up-and-downs).

**Ownership guard** (`EnsureRoundAccess`): one `IRoundService.GetRoundOwnerIdAsync` query; 404 if the round does not exist, 403 if another golfer owns it.

### Reads

| Method + route | Request | Response | Status codes | Calls |
|---|---|---|---|---|
| `GET /api/rounds` | query `courseId?`, `fullRoundOnly?`, `startDate?`, `endDate?` | `RoundResponse[]` (summary, no shots) | 200 | `IRoundService.GetRoundsByUserIdAsync(userId[, filter])` |
| `GET /api/rounds/details` | query `courseId?`, `fullRoundOnly?`, `startDate?`, `endDate?`, `year?`, `level?` | `RoundResponse[]` with holes, shots and strokes gained | 200 | `IRoundService.GetRoundsWithDetailsAsync(userId, filter, level)` |
| `GET /api/rounds/{roundId}` | query `level?` | `RoundResponse` | 200; 403; 404 | `EnsureRoundAccess`, `GetRoundByIdAsync(roundId, level)`. Not gated on `IsComplete`, so the owner can read an in-progress round |
| `GET /api/rounds/active` | query `level?` | `RoundResponse` | 200; **204** when there is no open round (asked on every app launch, so not an error) | `IRoundEntryService.GetActiveRoundAsync` |
| `GET /api/rounds/{roundId}/shots` | none | `Dictionary<scoreId, ShotData[]>` (keyed by `ScoreId`) | 200; 403; 404 | `EnsureRoundAccess`, `GetShotsByRoundIdAsync` |
| `GET /api/rounds/courses` | none | `CourseResponse[]`: `CourseId`, `CourseName` (courses the golfer has played) | 200 | `GetPlayedCoursesByUserId` |

The list and stats reads only include completed rounds (`IsComplete == true`).

### Hole-by-hole entry

| Method + route | Request | Response | Status codes | Calls |
|---|---|---|---|---|
| `POST /api/rounds/start` | `StartRoundRequest`: `CourseId`, `TeeboxId`, `DatePlayed`, `FullRound`/`FrontNine`/`BackNine` (one required), `UsingHoleStats`, `UsingShotTracking`. Validated | `StartRoundResponse`: `RoundId`, `TeeboxId`, `Holes` (`HoleInfo[]`) | 201 (`Location: /api/rounds/{id}`); 409 `{ error: "ActiveRoundExists", message, roundId }` (not ProblemDetails); 409 archived teebox; 404 teebox | `IRoundEntryService.StartRoundAsync` |
| `PUT /api/rounds/{roundId}/holes/{holeNumber}` | `holeNumber` 1–18 (route constraint). `UpsertHoleRequest`: `Score`, hole stats (`HitFairway?`, `MissFairwayType?`, `HitGreen?`, `MissGreenType?`, `NumberOfPutts?`, `ApproachYardage?`, out-of-position and penalty flags), `Shots?` (`ShotData[]`). No par: it comes from the teebox. Validated | `RoundProgressResponse`: `RoundId`, `HoleNumber`, `HoleId`, `ScoreId`, `Par`, `Score?`, `ScoreOut`, `ScoreIn`, `Total`, `HolesEntered`, `ParEntered`, `ToPar` | 200; 403 not owner; 404 round or hole not on teebox; 409 round already posted | `IRoundEntryService.UpsertHoleAsync` |
| `DELETE /api/rounds/{roundId}/holes/{holeNumber}` | none | `RoundProgressResponse` | same as PUT | `IRoundEntryService.ClearHoleAsync` |
| `POST /api/rounds/{roundId}/complete` | query `level?` | `RoundResponse` (now `IsComplete = true`) | 200; 400 `ValidationProblemDetails` with `errors.Holes` naming the missing hole numbers; 403; 404 | `IRoundEntryService.CompleteRoundAsync` |

`MapHoleResult` maps `RoundEntryStatus` for both per-hole routes: `NotOwner` → 403, `HoleNotOnTeebox` → 404 Hole, `RoundAlreadyComplete` → 409 (detail points to `PUT /api/rounds/{id}`), anything else → 404 Round.

### Whole-round writes

| Method + route | Request | Response | Status codes | Calls |
|---|---|---|---|---|
| `POST /api/rounds` **(deprecated)** | `CreateRoundRequest`: `CourseId`, `TeeboxId`, `DatePlayed`, `FullRound`/`FrontNine`/`BackNine`, `UsingHoleStats`, `UsingShotTracking`, `Holes` (`HoleScoreEntry[]`: `HoleId`, `HoleNumber`, `Par`, `Score`, hole stats, `Shots?`). `UserId` is overwritten from the token. Validated | new round id (`long`) | 201 | `IRoundService.CreateRoundAsync` |
| `PUT /api/rounds/{roundId}` | `UpdateRoundRequest`: `TeeboxId`, `DatePlayed`, `UsingHoleStats`, `UsingShotTracking`, `Holes` (`HoleScoreEntry[]`). `RoundId` and `UserId` are overwritten from the route and token. Validated. Replaces the whole hole set | none | 204; 403; 404; 409 if the round is still in progress (it would fight the per-hole writer) | `EnsureRoundAccess`, `GetRoundByIdAsync`, `UpdateRoundAsync` |
| `PATCH /api/rounds/{roundId}/exclude-from-stats` | `SetExcludeFromStatsRequest`: `ExcludeFromStats` | none | 204; 403; 404 | `EnsureRoundAccess`, `SetExcludeFromStatsAsync` |
| `DELETE /api/rounds/{roundId}` | none | none | 204; 403; 404 | `EnsureRoundAccess`, `DeleteRoundAsync` |

## Stats (`Endpoints/StatsEndpoints.cs`)

Response shapes are covered in [../features/stats-and-strokes-gained.md](../features/stats-and-strokes-gained.md).

| Method + route | Query | Response | Status codes | Calls |
|---|---|---|---|---|
| `GET /api/stats` | `fullRoundOnly?`, `startDate?`, `endDate?`, `courseId?`, `year?`, `level?` | `UserStatsResponse`: `TotalRounds`, `Scoring`, `BallStriking`, `ShortGame`, `ParTypeScoring`, `ScoringDistribution`, `StrokesGained`, `MostPlayedCourses` | 200 | `IStatsService.GetUserStatsAsync(userId, filter, level: level)` |
| `GET /api/stats/courses/{courseId}` | `teeboxId?`, `startDate?`, `endDate?`, `fullRoundOnly?`, `year?`, `level?` | `CourseStatsResponse`: course id/name, `TotalRounds`, `Scoring`, `ScoringDistribution`, `StrokesGained` (with `PerHole`), `BallStriking`, `ShortGame`, `ParTypeScoring`, `HoleStats`, `TeeboxOptions`, `SelectedTeeboxId` | 200; 404 `CourseStats` | `GetCourseStatsAsync` |
| `GET /api/stats/courses/{courseId}/holes` | same as above | `CourseHoleStatsResponse`: course id/name, `TotalRounds`, `Holes` (`HoleStatsDetail[]` with per-hole distribution, `Plays`, average SG, trend), `TeeboxOptions`, `SelectedTeeboxId` | 200; 404 `CourseHoleStats` | `GetCourseHoleStatsAsync` |
| `GET /api/stats/years` | none | `int[]` years with rounds | 200 | `GetAvailableYearsAsync` |
| `GET /api/stats/courses` | none | `CourseOption[]`: `CourseId`, `CourseName` | 200 | `GetUserCoursesAsync` |

## Lookups (`Endpoints/LookupEndpoints.cs`)

| Method + route | Response | Source |
|---|---|---|
| `GET /api/lookups/miss-types` | `[{ missTypeId, missType1 }]` | `ApplicationDbContext.MissTypes`, queried directly in the endpoint (an exception to the service-layer rule) |
| `GET /api/lookups/lie-types` | `[{ value, name }]` for each `LieType` | enum |
| `GET /api/lookups/distance-units` | `[{ value: "yards", name: "Yards" }, { value: "feet", name: "Feet" }]` (values are the `DistanceUnit` string constants) | constants |

All 200, `User` auth.

## Profile (`Endpoints/ProfileEndpoints.cs`)

| Method + route | Request | Response | Status codes | Calls |
|---|---|---|---|---|
| `GET /api/profile` | none | `UserProfileResponse`: `UserProfileId`, `UserId`, `PublicIdentifier`, `IsPublic`, `PublicSlug?`, `ShowStrokesGained`, `ShowRecentRounds`, `ShowCourseNames`, `DisplayName?`, `Username`, `Email?`, `PreferredTees`, `SgBaselineLevel` | 200 (creates the profile on first access) | `IProfileService.GetOrCreateProfileAsync` |
| `PUT /api/profile/sg-baseline-level` | `UpdateSgBaselineLevelRequest` (record in the same file): `{ "level": "Hcp10" }` | none | 204; 400 for an unknown level name (binding) | `IProfileService.UpdateSgBaselineLevelAsync` |
| `PUT /api/profile/username` | `UpdateUsernameRequest` (record in the same file): `{ "username": "tee.time_42" }`. Validated (non-empty) | `UsernameResponse`: `{ "username" }`, normalized (lowercase, leading `@` dropped) | 200, including when unchanged; 400 ProblemDetails for an invalid or reserved name; 409 ProblemDetails when someone else has it | `IUsernameService.ChangeUsernameAsync` |
| `PUT /api/profile/public` | `UpdatePublicProfileRequest` (record in the same file): `{ "isPublic": true, "slug": "george", "showStrokesGained": true, "showRecentRounds": true, "showCourseNames": true }`. The `show*` flags default to true; a blank `slug` keeps the current one, or derives one from the username the first time the profile goes public | `UserProfileResponse` | 200; 400 ProblemDetails for an invalid or reserved slug; 409 ProblemDetails when someone else has it (any case) | `IProfileService.UpdatePublicProfileAsync(userId, …, actingUserId: userId)` |
| `GET /api/profile/username-available` | query `username` | `UsernameAvailability`: `Username` (normalized), `Available`, `Reason?` | 200. Your own current username counts as available | `IUsernameService.CheckAvailabilityAsync(username, userId)` |

Profiles and visibility: [../features/users-and-social.md](../features/users-and-social.md). The public stats page: [../features/public-profile.md](../features/public-profile.md).

## Friends (`Endpoints/FriendEndpoints.cs`)

`FriendResponse`: `FriendshipId`, `UserId`, `PublicIdentifier`, `DisplayName`, `Username`, `Email`, `IsPublic`, `FriendsSince`.
`FriendRequestResponse`: `FriendshipId`, `OtherUserId`, `OtherPublicIdentifier`, `OtherDisplayName`, `OtherUsername`, `OtherEmail`, `Direction`, `RequestedOn`.

| Method + route | Request | Response | Status codes | Calls |
|---|---|---|---|---|
| `GET /api/friends` | none | `FriendResponse[]` | 200 | `IFriendService.GetFriendsAsync` |
| `GET /api/friends/requests/incoming` | none | `FriendRequestResponse[]` | 200 | `GetIncomingRequestsAsync` |
| `GET /api/friends/requests/incoming/count` | none | `int` | 200 | `GetIncomingRequestCountAsync` |
| `GET /api/friends/requests/outgoing` | none | `FriendRequestResponse[]` | 200 | `GetOutgoingRequestsAsync` |
| `GET /api/friends/search` | query `query` (≥ 2 chars after trim; a leading `@` searches usernames only, by prefix), `take?` (default 20) | `UserSearchResultResponse[]`: `UserId`, `PublicIdentifier`, `DisplayName`, `Username`, `Email`, `FriendshipState`, `FriendshipId?` | 200; 400 `{ error }` for a short query | `SearchUsersAsync(userId, query, take)` |
| `POST /api/friends/requests` | `SendFriendRequestRequest`: `AddresseeUserId`. Validated | new friendship id (`long`) | 201 (`Location: /api/friends/requests/{id}`); 409 `{ error }` for self, unknown user, already friends, already pending | `SendRequestAsync`. If the other golfer already sent a request, it is auto-accepted |
| `PUT /api/friends/requests/{friendshipId}/accept` | none | none | 204; 404 `FriendRequest` | `AcceptRequestAsync(friendshipId, userId)` |
| `PUT /api/friends/requests/{friendshipId}/reject` | none | none | 204; 404 | `RejectRequestAsync` |
| `DELETE /api/friends/requests/{friendshipId}` | none | none | 204; 404 (cancel your own outgoing request) | `CancelRequestAsync` |
| `DELETE /api/friends/{friendshipId}` | none | none | 204; 404 `Friendship` | `RemoveFriendAsync` |

### Viewing a friend's activity

These take the friend's `publicId` (a `Guid`, the profile's `PublicIdentifier`), never a user id. `EnsureFriendAccessAsync` resolves it with `IProfileService.GetUserIdByPublicIdIgnoringVisibilityAsync` and requires `IFriendService.AreFriendsAsync` (or that it is the viewer's own id). A stranger gets 404 `Profile`, not 403, so the response does not confirm the profile exists.

| Method + route | Response | Status codes | Calls |
|---|---|---|---|
| `GET /api/friends/{publicId}/rounds` | `RoundResponse[]` | 200; 404 | `IRoundService.GetRoundsByUserIdAsync(friendId)` |
| `GET /api/friends/{publicId}/rounds/{roundId}` | `RoundResponse` | 200; 404 if the round is not the friend's **or is still in progress** | `IsRoundOwnedByUserAsync`, `GetRoundByIdAsync` |
| `GET /api/friends/{publicId}/stats` | `UserStatsResponse` (default filter and level) | 200; 404 | `IStatsService.GetUserStatsAsync(friendId)` |

## Games (`Endpoints/GameEndpoints.cs`)

Side games (match play, skins, high-low) played across several golfers' cards. The rules, engines and scoreboard are in [../features/games.md](../features/games.md); this section lists the HTTP surface. Every mutating route returns the full `GameStateResponse` so the app can redraw from one response.

`GameStateResponse`: `GameId`, `GameType`, `State` (`Setup`, `Active`, `Completed`, `Abandoned`), `CourseId`, `CourseName`, `DatePlayed`, `HostUserId`, `JoinCode`, `Rules`, `HoleNumbers`, `Participants` (`GameParticipantResponse[]`: `ParticipantId`, `UserId?`, `PublicIdentifier?`, `DisplayName`, `IsGuest`, `IsHost`, `RoundId?`, `TeeboxId`, `TeeboxName`, `CourseHandicap`, `PlayingHandicap`, `Team?`, `HolesEntered`, `RoundUnavailable`, `RoundIsComplete`, `RoundReadyToPost`, `Holes` of `{ HoleNumber, Strokes }`), `Scoreboard?`.

| Method + route | Request | Response | Status codes | Calls |
|---|---|---|---|---|
| `POST /api/games` | `CreateGameRequest`: `GameType`, `CourseId`, `TeeboxId`, `DatePlayed`, `FullRound` (default true)/`FrontNine`/`BackNine`, `CourseHandicap`, `RoundId?`, `Team?`, `UseNet`, `HandicapAllowancePercent` (default 100), `StrokesOffLow` (default true), `SkinsCarryover` (default true), `SkinsValue?`. Validated | `GameStateResponse` | 201 (`Location: /api/games/{id}`); 404/409 per mapping | `IGameService.CreateGameAsync(request, userId)` |
| `GET /api/games` | query `activeOnly?` (default true: `Setup` or `Active` only) | `GameSummaryResponse[]`: `GameId`, `GameType`, `State`, `CourseId`, `CourseName`, `DatePlayed`, `JoinCode`, `IsHost`, `ParticipantCount` | 200 | `GetMyGamesAsync` |
| `GET /api/games/{gameId}` | header `If-None-Match?` | `GameStateResponse`, header `ETag: W/"..."` | 200; **304** when the ETag matches; 404 if not a participant | `GetGameAsync`, then `WithETag` |
| `GET /api/games/preview` | query `joinCode` | `GameJoinPreviewResponse`: `GameId`, `GameType`, `State`, `CourseId`, `CourseName`, `DatePlayed`, `HoleNumbers`, `HostDisplayName`, `ParticipantCount`, `AlreadyJoined` | 200; 404 invalid code | `PreviewGameAsync` (does not join) |
| `POST /api/games/join` | `JoinGameRequest`: `JoinCode` (≤ 12), `TeeboxId`, `CourseHandicap`, `RoundId?`, `Team?`. Validated | `GameStateResponse` | 200; 404; 409 (already joined, wrong course teebox, ...) | `JoinGameAsync` |
| `POST /api/games/{gameId}/participants` | `AddParticipantRequest`: `UserId?` (a friend) or `DisplayName?` (a guest), `TeeboxId`, `CourseHandicap`, `Team?`. Validated | `GameStateResponse` | 200; 403 not host; 404; 409 (e.g. `NotFriends`) | `AddParticipantAsync` (host only) |
| `PUT /api/games/{gameId}/participants/{participantId}` | `UpdateParticipantRequest`: `CourseHandicap?` (-10 to 54), `TeeboxId?` (> 0), `Team?`, `DisplayName?` (≤ 128); null leaves a field unchanged. Validated | `GameStateResponse` | 200; 403 (not host and not that participant); 404; 409 | `UpdateParticipantAsync` |
| `DELETE /api/games/{gameId}/participants/{participantId}` | none | `GameStateResponse` | 200; 403; 404; 409 | `RemoveParticipantAsync` (host only) |
| `POST /api/games/{gameId}/round` | `LinkRoundRequest`: `RoundId?` (`null` unlinks, otherwise > 0). Validated | `GameStateResponse` | 200; 404; 409 (round not owned, not on the game's course, not active) | `LinkRoundAsync` |
| `PUT /api/games/{gameId}/participants/{participantId}/holes/{holeNumber}` | `holeNumber` 1–18. `UpsertGameHoleRequest`: `Strokes` (> 0). Validated | `GameStateResponse` | 200; 403; 404 (participant, or hole not in game); 409 (game not active, participant scores via linked round) | `UpsertParticipantHoleAsync` |
| `DELETE /api/games/{gameId}/participants/{participantId}/holes/{holeNumber}` | none | `GameStateResponse` | as above | `ClearParticipantHoleAsync` |
| `POST /api/games/{gameId}/start` | none | `GameStateResponse` | 200; 403; 404; 409 (not in setup, wrong participant count, ...) | `StartGameAsync` (host only) |
| `POST /api/games/{gameId}/complete` | none | `GameStateResponse` | 200; 403; 404; 409 | `CompleteGameAsync` (host only) |
| `POST /api/games/{gameId}/abandon` | none | `GameStateResponse` | 200; 403; 404; 409 | `AbandonGameAsync` (host only) |

`/preview` and `/join` are literal segments; the `{gameId:long}` constraint on the sibling route keeps them from being parsed as ids.

`MapGameResult` maps `GameResultStatus` the same way on every route:

| Status | HTTP |
|---|---|
| `Ok` | 200 with the value (201 for create) |
| `GameNotFound`, `NotParticipant` | 404 `Game` (a non-participant cannot learn the game exists) |
| `JoinCodeInvalid` | 404 |
| `ParticipantNotFound` | 404 `GameParticipant` |
| `HoleNotInGame` | 404 `Hole` |
| `NotHost` | 403 |
| `GameNotInSetup`, `GameNotActive`, `GameAlreadyComplete`, `AlreadyJoined`, `RoundNotOwned`, `RoundNotOnGameCourse`, `RoundNotActive`, `ParticipantCountInvalid`, `GameShapeUnsupported`, `TeeboxArchived`, `TeeboxNotOnCourse`, `TeeboxShapeMismatch`, `ParticipantUsesLinkedRound`, `NotFriends`, `GameTypeNotSupported` | 409, `detail` = the service's explanation |
| anything else | 404 `Game` |

## Devices (`Endpoints/DeviceEndpoints.cs`)

APNs device registration; delivery is covered in [../features/notifications-and-email.md](../features/notifications-and-email.md).

| Method + route | Auth | Request | Response | Status codes | Calls |
|---|---|---|---|---|---|
| `POST /api/devices` | User | `RegisterDeviceRequest`: `DeviceToken` (≤ 256), `DeviceName?` (≤ 200). Validated | none | 204 | `IPushNotificationService.RegisterDeviceAsync(userId, token, name)` (upsert by token) |
| `DELETE /api/devices/{token}` | User | route `token` | none | 204 always | `UnregisterDeviceAsync(userId, token)` (marks inactive only if the token is the caller's) |
| `POST /api/devices/test` | Admin | `SendTestPushRequest`: `TargetUserId`, `Title` (≤ 200), `Body` (≤ 1000), `Badge?`. Validated | `SendTestPushResponse`: `Sent` (devices delivered to) | 200; 403 non-admin | `SendToUserAsync` |

`DELETE /api/devices/{token}` only deactivates a token registered to the caller. A token that is unknown or belongs to someone else is left alone, and the answer is the same 204 either way, so the endpoint cannot be used to discover or switch off another golfer's device.

## Admin invites (`Endpoints/AdminInviteEndpoints.cs`)

Requires the `Admin` role; a golfer gets 403. The admin console has its own invite page that calls the same service ([../admin/console.md](../admin/console.md)).

| Method + route | Request | Response | Status codes | Calls |
|---|---|---|---|---|
| `GET /api/admin/invites` | none | `PendingInviteDto[]`: `Id`, `Email`, `CreatedOn`, `ExpiresOn`, `ClaimedOn?`, `IsExpired` | 200 | `IUserInvitationService.GetPendingInvitesAsync` |
| `POST /api/admin/invites` | `SendInviteRequest`: `Email`. Validated | none | 204; 400 ProblemDetails with the service error | `CreateAndSendInviteAsync(email, adminUserId)` |
| `DELETE /api/admin/invites/{id}` | `id` int | none | 204; 404 (empty body) | `RevokeInviteAsync(id, adminUserId)` |

## Public profile (`Endpoints/PublicProfileEndpoints.cs`)

The group is `AllowAnonymous()` and `RequireRateLimiting(PublicProfilePolicy)`. Its consumer is the Landing site's build, which bakes each page into `fairwayfinder.pro/golf/{slug}` ([../features/public-profile.md](../features/public-profile.md)).

| Method + route | Request | Response | Status codes | Calls |
|---|---|---|---|---|
| `GET /api/public/profiles/{slug}` | `slug` (case-insensitive) | `PublicProfileResponse`: `DisplayName`, `Slug`, `GeneratedOn`, `Headline` (`HandicapIndex?` estimate, `ScoringAverage18?`, `RoundsThisYear`, `TotalRounds`, `BestRound?`), `StrokesGained?`, `ScoringDistribution`, `Trend` (last 20 18-hole rounds, oldest first), `RecentRounds?` (last 5, each with a scorecard `Key`), `Scorecards` (hole-by-hole cards for the recent rounds and the best round, keyed by date, with teebox rating/slope and per-hole yardage, handicap, score and tracked fairway/green/putts and miss direction; empty when recent rounds are hidden). Hidden sections are null; with course names hidden every `CourseName` is null. No user id, email, username or public identifier | 200 with `Cache-Control: public, max-age=300`; 404 `PublicProfile` for an unknown slug **and** a private profile alike | `IPublicProfileService.GetBySlugAsync` |

## Public web surface (`Program.cs`)

| Method + route | Auth | Response |
|---|---|---|
| `GET /.well-known/apple-app-site-association` | Anon | AASA JSON (`webcredentials`, `applinks` for `/register*` and `/reset-password*`) |
| `GET /register` | Anon | HTML landing page for invite links |
| `GET /reset-password` | Anon | HTML landing page for password reset links |

Details in [overview.md](overview.md#public-web-surface).

## Development-only routes

| Route | Source |
|---|---|
| `GET /health`, `GET /alive` | `MapDefaultEndpoints()` in ServiceDefaults; mapped only in Development |
| `GET /openapi/v1.json`, `/scalar` | `MapOpenApi`, `MapScalarApiReference` in `Program.cs`; Development only |
