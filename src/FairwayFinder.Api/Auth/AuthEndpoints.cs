using FairwayFinder.Api.Exceptions;
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
