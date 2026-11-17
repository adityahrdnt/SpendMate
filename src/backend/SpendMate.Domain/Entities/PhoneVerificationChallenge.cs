namespace SpendMate.Domain.Entities;

public class PhoneVerificationChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public required string PhoneNumber { get; set; }

    public required string CodeHash { get; set; }

    public required string CodeCiphertext { get; set; }

    public required string EncryptionNonce { get; set; }

    public required string EncryptionTag { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset LastSentAtUtc { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }

    public int Attempts { get; set; }

    public User User { get; set; } = null!;
}
