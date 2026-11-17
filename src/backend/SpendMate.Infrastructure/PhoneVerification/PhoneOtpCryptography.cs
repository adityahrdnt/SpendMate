using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Auth.Options;

namespace SpendMate.Infrastructure.PhoneVerification;

public sealed class PhoneOtpCryptography(
    IOptions<PhoneOtpOptions> options)
    : IPhoneOtpCryptography
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _hashingKey =
        Convert.FromBase64String(options.Value.HashingSecret);

    private readonly byte[] _encryptionKey =
        Convert.FromBase64String(options.Value.EncryptionKey);

    public string ComputeHash(string code)
    {
        using var hmac = new HMACSHA256(_hashingKey);

        var hash = hmac.ComputeHash(
            Encoding.UTF8.GetBytes(code));

        return Convert.ToHexString(hash);
    }

    public bool VerifyHash(
        string code,
        string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        try
        {
            var actualHash = Convert.FromHexString(
                ComputeHash(code));

            var expectedHashBytes =
                Convert.FromHexString(expectedHash);

            return actualHash.Length == expectedHashBytes.Length &&
                CryptographicOperations.FixedTimeEquals(
                    actualHash,
                    expectedHashBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public EncryptedPhoneOtp Encrypt(string code)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintext = Encoding.UTF8.GetBytes(code);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_encryptionKey, TagSize);

        aes.Encrypt(
            nonce,
            plaintext,
            ciphertext,
            tag);

        return new EncryptedPhoneOtp(
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag));
    }

    public string Decrypt(
        string ciphertext,
        string nonce,
        string tag)
    {
        var ciphertextBytes =
            Convert.FromBase64String(ciphertext);

        var nonceBytes =
            Convert.FromBase64String(nonce);

        var tagBytes =
            Convert.FromBase64String(tag);

        var plaintext = new byte[ciphertextBytes.Length];

        using var aes = new AesGcm(_encryptionKey, TagSize);

        aes.Decrypt(
            nonceBytes,
            ciphertextBytes,
            tagBytes,
            plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
