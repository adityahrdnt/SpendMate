namespace SpendMate.Application.Auth.Options;

public sealed class PhoneOtpOptions
{
    public const string SectionName = "PhoneOtp";

    public string HashingSecret { get; set; } = string.Empty;

    public string EncryptionKey { get; set; } = string.Empty;

    public int CodeLength { get; set; } = 6;

    public int ExpiryMinutes { get; set; } = 5;

    public int MaxAttempts { get; set; } = 5;

    public int ResendCooldownSeconds { get; set; } = 60;
}
