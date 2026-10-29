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
                return AccessDenied();
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
                return AccessDenied();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPut("/{expenseId:guid}", async (
            Guid familyId,
            Guid expenseId,
            UpdateExpenseRequest request,
            ExpenseService expenseService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var expense = await expenseService.UpdateAsync(
                    familyId,
                    expenseId,
                    request,
                    cancellationToken);

                return Results.Ok(expense);
            }
            catch (UnauthorizedAccessException)
            {
                return AccessDenied();
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Expense not found",
                    detail: "The requested expense was not found in this family.");
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapDelete("/{expenseId:guid}", async (
            Guid familyId,
            Guid expenseId,
            ExpenseService expenseService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await expenseService.DeleteAsync(
                    familyId,
                    expenseId,
                    cancellationToken);

                return Results.NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return AccessDenied();
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Expense not found",
                    detail: "The requested expense was not found in this family.");
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }

    private static IResult AccessDenied()
    {
        return Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Access denied",
            detail: "You are not a member of this family.");
    }
}
