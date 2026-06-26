using FairwayFinder.Api.Exceptions;
using FairwayFinder.Api.Validators;
using FairwayFinder.Features.Data;
using FairwayFinder.Features.Services.Interfaces;
using FairwayFinder.Identity;
using Microsoft.AspNetCore.Identity;

namespace FairwayFinder.Api.Auth;

public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        group.MapPost("/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            JwtTokenService tokenService) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
                return Results.Unauthorized();

            var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
                return Results.Problem(
                    statusCode: StatusCodes.Status423Locked,
                    title: "Account locked",
                    detail: "This account has been locked due to too many failed attempts. Please try again later.");

            if (!result.Succeeded)
                return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(user);
            var (accessToken, expiresAt) = tokenService.GenerateAccessToken(user, roles);
            var refreshToken = await tokenService.IssueRefreshTokenAsync(user.Id, request.DeviceName);

            return Results.Ok(BuildLoginResponse(accessToken, refreshToken, expiresAt, user));
        });

        group.MapPost("/refresh", async (
            RefreshRequest request,
            UserManager<ApplicationUser> userManager,
            JwtTokenService tokenService) =>
        {
            var rotation = await tokenService.RotateRefreshTokenAsync(request.RefreshToken);
            if (rotation is null)
                return Results.Unauthorized();

            var user = await userManager.FindByIdAsync(rotation.UserId);
            if (user is null)
                return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(user);
            var (accessToken, expiresAt) = tokenService.GenerateAccessToken(user, roles);

            return Results.Ok(BuildLoginResponse(accessToken, rotation.RefreshToken, expiresAt, user));
        });

        group.MapPost("/logout", async (
            RefreshRequest request,
            JwtTokenService tokenService) =>
        {
            await tokenService.RevokeRefreshTokenAsync(request.RefreshToken);
            return Results.NoContent();
        });

        // ── Invite-only registration ────────────────────────────
        group.MapGet("/invites/{code}", async (
            string code,
            IUserInvitationService inviteService) =>
        {
            var result = await inviteService.ValidateInviteAsync(code);
            return Results.Ok(result);
        });

        group.MapPost("/register", async (
            RegisterRequest request,
            UserManager<ApplicationUser> userManager,
            IUserInvitationService inviteService,
            IProfileService profileService,
            JwtTokenService tokenService,
            ILoggerFactory loggerFactory) =>
        {
            // The email is taken from the invite, never the request — this enforces
            // that the account always matches who was invited.
            var validation = await inviteService.ValidateInviteAsync(request.Code);
            if (!validation.Valid || validation.Email is null)
                return Results.Problem(
                    detail: validation.Reason ?? "This invitation is not valid.",
                    statusCode: StatusCodes.Status400BadRequest);

            var now = DateTime.UtcNow;
            var user = new ApplicationUser
            {
                UserName = validation.Email,
                Email = validation.Email,
                EmailConfirmed = true,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                PreferredTees = (PreferredTees)request.PreferredTees,
                CreatedOn = now,
                UpdatedOn = now
            };

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return Results.Problem(
                    detail: string.Join(" ", result.Errors.Select(e => e.Description)),
                    statusCode: StatusCodes.Status400BadRequest);

            await userManager.AddToRoleAsync(user, ApplicationRoles.User);
            await inviteService.ClaimInviteAsync(request.Code);

            // Create the user's profile up front so they are immediately searchable as a friend.
            // Best-effort: a failure here must not fail registration (the profile is also
            // created lazily on first profile access).
            try
            {
                await profileService.GetOrCreateProfileAsync(user.Id);
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("AuthEndpoints")
                    .LogError(ex, "Failed to create profile for new user {UserId} during registration.", user.Id);
            }

            var roles = await userManager.GetRolesAsync(user);
            var (accessToken, expiresAt) = tokenService.GenerateAccessToken(user, roles);
            var refreshToken = await tokenService.IssueRefreshTokenAsync(user.Id, request.DeviceName);

            return Results.Ok(BuildLoginResponse(accessToken, refreshToken, expiresAt, user));
        })
        .AddEndpointFilter<ValidationFilter<RegisterRequest>>();

        return app;
    }

    private static LoginResponse BuildLoginResponse(string accessToken, string refreshToken, DateTime expiresAt, ApplicationUser user) => new()
    {
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        ExpiresAt = expiresAt,
        UserId = user.Id,
        Email = user.Email!,
        FirstName = user.FirstName,
        LastName = user.LastName
    };
}
