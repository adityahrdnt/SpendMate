using Microsoft.AspNetCore.Antiforgery;

namespace SpendMate.Api.Endpoints;

public static class AntiforgeryEndpoints
{
    public static IEndpointRouteBuilder MapAntiforgeryEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/csrf", (
            IAntiforgery antiforgery,
            HttpContext httpContext) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(httpContext);

            return Results.Ok(new
            {
                requestToken = tokens.RequestToken
            });
        })
        .AllowAnonymous()
        .WithTags("Authentication");

        return app;
    }
}
