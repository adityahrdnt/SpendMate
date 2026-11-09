using System.Security.Claims;
using SpendMate.Application.Abstractions;

namespace SpendMate.Api.Security;

public sealed class ClaimsCurrentUser(
    IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var userId = httpContextAccessor.HttpContext?
                .User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userId, out var parsedUserId))
            {
                throw new InvalidOperationException(
                    "The authenticated user does not have a valid user ID claim.");
            }

            return parsedUserId;
        }
    }
}
