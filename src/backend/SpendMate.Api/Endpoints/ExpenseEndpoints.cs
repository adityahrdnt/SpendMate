using SpendMate.Application.Expenses.Dtos;
using SpendMate.Application.Expenses.Services;

namespace SpendMate.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/families/{familyId:guid}/expenses")
            .WithTags("Expenses");

        group.MapGet("/", async (
            Guid familyId,
            ExpenseService expenseService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var expenses = await expenseService.GetByFamilyAsync(
                    familyId, cancellationToken);

                return Results.Ok(expenses);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Access denied",
                    detail: "You are not a member of this family.");
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/", async (
            Guid familyId,
            CreateExpenseRequest request,
            ExpenseService expenseService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var expense = await expenseService.CreateAsync(
                    familyId, request, cancellationToken);

                return Results.Created(
                    $"/api/families/{familyId}/expenses/{expense.Id}",
                    expense);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Access denied",
                detail: "You are not a member of this family.");
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }
}
