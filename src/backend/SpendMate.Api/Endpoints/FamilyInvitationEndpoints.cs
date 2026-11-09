using SpendMate.Application.Families.Dtos;
using SpendMate.Application.Families.Services;

namespace SpendMate.Api.Endpoints;

public static class FamilyInvitationEndpoints
{
    public static IEndpointRouteBuilder MapFamilyInvitationEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/families/{familyId:guid}/invitations")
            .WithTags("Family Invitations")
            .RequireAuthorization();

        group.MapPost("/", async (
            Guid familyId,
            CreateFamilyInvitationRequest request,
            FamilyInvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await invitationService.CreateAsync(
                    familyId,
                    request,
                    cancellationToken);

                return Results.Json(
                    result.Invitation,
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
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        group.MapDelete("/{invitationId:guid}", async (
            Guid familyId,
            Guid invitationId,
            FamilyInvitationService invitationService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var invitation = await invitationService.RevokeAsync(
                    familyId,
                    invitationId,
                    cancellationToken);

                return Results.Ok(invitation);
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

        return app;
    }
}
