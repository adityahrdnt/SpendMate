using SpendMate.Application.Abstractions;

namespace SpendMate.Api.Development;

public static class DevelopmentIds
{
    public static readonly Guid UserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid FamilyId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
}

public sealed class DevelopmentCurrentUser : ICurrentUser
{
    public Guid UserId => DevelopmentIds.UserId;
}
