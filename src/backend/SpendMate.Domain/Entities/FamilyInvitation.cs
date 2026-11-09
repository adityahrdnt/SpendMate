using SpendMate.Domain.Enums;

namespace SpendMate.Domain.Entities;

public class FamilyInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FamilyId { get; set; }

    public Guid InvitedByUserId { get; set; }

    public required string Email { get; set; }

    public required string NormalizedEmail { get; set; }

    public required string TokenHash { get; set; }

    public FamilyInvitationStatus Status { get; set; } =
        FamilyInvitationStatus.Pending;

    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public Guid? AcceptedByUserId { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public Family Family { get; set; } = null!;

    public User InvitedByUser { get; set; } = null!;

    public User? AcceptedByUser { get; set; }
}
