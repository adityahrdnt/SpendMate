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

        // Get members of a shared family
        group.MapGet("/{familyId:guid}/members", async (
            Guid familyId,
            FamilyService familyService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var members = await familyService.GetMembersAsync(
                    familyId,
                    cancellationToken);

                return Results.Ok(members);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
        });

        // Update a family member's role
        group.MapPatch("/{familyId:guid}/members/{userId:guid}/role", async (
            Guid familyId,
            Guid userId,
            UpdateFamilyMemberRoleRequest request,
            FamilyService familyService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await familyService.UpdateMemberRoleAsync(
                    familyId,
                    userId,
                    request,
                    cancellationToken);

                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        // Remove a member from a shared family
        group.MapDelete("/{familyId:guid}/members/{userId:guid}", async (
            Guid familyId,
            Guid userId,
            FamilyService familyService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await familyService.RemoveMemberAsync(
                    familyId,
                    userId,
                    cancellationToken);

                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Unauthorized();
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
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
