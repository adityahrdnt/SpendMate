namespace SpendMate.Application.Abstractions;

public interface IFamilyMembershipRepository
{
    Task<bool> IsMemberAsync(
        Guid familyId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
