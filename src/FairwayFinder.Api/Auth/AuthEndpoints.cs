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
            var refreshToken = tokenService.GenerateRefreshToken(user.Id);

            return Results.Ok(BuildLoginResponse(accessToken, refreshToken, expiresAt, user));
        });

        group.MapPost("/refresh", async (
            RefreshRequest request,
            UserManager<ApplicationUser> userManager,
            JwtTokenService tokenService) =>
        {
            var userId = tokenService.ValidateRefreshToken(request.RefreshToken);
            if (userId is null)
                return Results.Unauthorized();

            var user = await userManager.FindByIdAsync(userId);
            if (user is null)
                return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(user);
            var (accessToken, expiresAt) = tokenService.GenerateAccessToken(user, roles);
            var newRefreshToken = tokenService.GenerateRefreshToken(user.Id);

            return Results.Ok(BuildLoginResponse(accessToken, newRefreshToken, expiresAt, user));
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
