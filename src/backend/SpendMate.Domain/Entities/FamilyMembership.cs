using SpendMate.Domain.Enums;

namespace SpendMate.Domain.Entities;

public class FamilyMembership
{
    public Guid FamilyId { get; set; }

    public Guid UserId { get; set; }

    public FamilyRole Role { get; set; } = FamilyRole.Member;

    public DateTimeOffset JoinedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public Family Family { get; set; } = null!;

    public User User { get; set; } = null!;
}
