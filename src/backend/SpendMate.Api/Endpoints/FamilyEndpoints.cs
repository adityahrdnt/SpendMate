using SpendMate.Application.Families.Dtos;
using SpendMate.Application.Families.Services;

namespace SpendMate.Api.Endpoints;

public static class FamilyEndpoints
{
    public static IEndpointRouteBuilder MapFamilyEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/families")
            .WithTags("Families")
            .RequireAuthorization();

        // Get all families for the current user
        group.MapGet("/", async (
            FamilyService familyService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var families = await familyService.GetAllAsync(
                    cancellationToken);

                return Results.Ok(families);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });

        // Create a shared family
        group.MapPost("/", async (
            CreateFamilyRequest request,
            FamilyService familyService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var family = await familyService.CreateAsync(
                    request,
                    cancellationToken);

                return Results.Json(
                    family,
                    statusCode: StatusCodes.Status201Created);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }
}
