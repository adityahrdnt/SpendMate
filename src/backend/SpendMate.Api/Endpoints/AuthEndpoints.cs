using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SpendMate.Application.Auth.Dtos;
using SpendMate.Application.Auth.Services;

namespace SpendMate.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        // Register
        group.MapPost("/register", async (
            RegisterRequest request,
            AuthService authService,
            HttpContext httpContext,
            CancellationToken cancellationToken,
            EmailVerificationService emailVerificationService,
            ILogger<Program> logger) =>
        {
            try
            {
                var authResponse = await authService.RegisterAsync(
                    request, cancellationToken);

                await SignInAsync(httpContext, authResponse);

                try
                {
                    await emailVerificationService.SendVerificationAsync(
                        authResponse.UserId,
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to send email verification for user {UserId}.",
                        authResponse.UserId);
                }

                return Results.Ok(authResponse);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .AllowAnonymous();

        // Login
        group.MapPost("/login", async (
            LoginRequest request,
            AuthService authService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var authResponse = await authService.LoginAsync(
                    request, cancellationToken);

                await SignInAsync(httpContext, authResponse);

                return Results.Ok(authResponse);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        })
        .AllowAnonymous();

        // Verify email
        group.MapPost("/verify-email", async (
            VerifyEmailRequest request,
            EmailVerificationService emailVerificationService,
            CancellationToken cancellationToken) =>
        {
            var verified = await emailVerificationService.VerifyAsync(
                request.Token,
                cancellationToken);

            return verified
                ? Results.Ok(new { message = "Email verified successfully." })
                : Results.BadRequest(new { error = "Invalid or expired verification token." });
        })
        .AllowAnonymous();

        // Resend email verification
        group.MapPost("/resend-verification", async (
            EmailVerificationService emailVerificationService,
            SpendMate.Application.Abstractions.ICurrentUser currentUser,
            CancellationToken cancellationToken) =>
        {
            await emailVerificationService.ResendVerificationAsync(
                currentUser.UserId,
                cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization();

        // Current authenticated user
        app.MapGet("/api/me", (HttpContext httpContext) =>
        {
            var user = httpContext.User;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = user.FindFirstValue(ClaimTypes.Email);
            var displayName = user.FindFirstValue(ClaimTypes.Name);
            var familyId = user.FindFirstValue("FamilyId");
            var familyName = user.FindFirstValue("FamilyName");

            if (!Guid.TryParse(userId, out var parsedUserId) ||
                !Guid.TryParse(familyId, out var parsedFamilyId) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(familyName))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new AuthResponse(
                parsedUserId,
                email,
                displayName,
                parsedFamilyId,
                familyName));
        })
        .RequireAuthorization()
        .WithTags("Authentication");

        // Logout
        group.MapPost("/logout", async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return Results.NoContent();
        })
        .RequireAuthorization();

        return app;
    }

    private static async Task SignInAsync(
        HttpContext httpContext,
        AuthResponse authResponse)
    {
        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                authResponse.UserId.ToString()),

            new Claim(
                ClaimTypes.Name,
                authResponse.DisplayName),

            new Claim(
                ClaimTypes.Email,
                authResponse.Email),

            new Claim(
                "FamilyId",
                authResponse.FamilyId.ToString()),

            new Claim(
                "FamilyName",
                authResponse.FamilyName)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal);
    }
}
