namespace SpendMate.Application.Abstractions;

public sealed record EncryptedPhoneOtp(
    string Ciphertext,
    string Nonce,
    string Tag);

public interface IPhoneOtpCryptography
{
    string ComputeHash(string code);

    bool VerifyHash(string code, string expectedHash);

    EncryptedPhoneOtp Encrypt(string code);

    string Decrypt(
        string ciphertext,
        string nonce,
        string tag);
}
