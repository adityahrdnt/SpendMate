using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SpendMate.Application.Abstractions;
using SpendMate.Application.Auth.Options;
using SpendMate.Infrastructure.PhoneVerification;

namespace SpendMate.UnitTests.PhoneVerification;

public class PhoneOtpCryptographyTests
{
    private static PhoneOtpCryptography CreateCryptography()
    {
        var options = Options.Create(new PhoneOtpOptions
        {
            HashingSecret = CreateKey("hashing-test-secret"),
            EncryptionKey = CreateKey("encryption-test-secret")
        });

        return new PhoneOtpCryptography(options);
    }

    private static string CreateKey(string value)
    {
        return Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    [Fact]
    public void ComputeHash_WhenCalled_ReturnsConsistentHash()
    {
        var cryptography = CreateCryptography();

        var firstHash = cryptography.ComputeHash("123456");
        var secondHash = cryptography.ComputeHash("123456");

        Assert.Equal(firstHash, secondHash);
        Assert.Equal(64, firstHash.Length);
    }

    [Fact]
    public void VerifyHash_WhenCodeIsCorrect_ReturnsTrue()
    {
        var cryptography = CreateCryptography();

        var hash = cryptography.ComputeHash("123456");

        var result = cryptography.VerifyHash("123456", hash);

        Assert.True(result);
    }

    [Fact]
    public void VerifyHash_WhenCodeIsIncorrect_ReturnsFalse()
    {
        var cryptography = CreateCryptography();

        var hash = cryptography.ComputeHash("123456");

        var result = cryptography.VerifyHash("654321", hash);

        Assert.False(result);
    }

    [Fact]
    public void Encrypt_WhenCalled_CanDecryptOriginalCode()
    {
        var cryptography = CreateCryptography();
        const string code = "123456";

        var encrypted = cryptography.Encrypt(code);

        var decrypted = cryptography.Decrypt(
            encrypted.Ciphertext,
            encrypted.Nonce,
            encrypted.Tag);

        Assert.Equal(code, decrypted);
    }

    [Fact]
    public void Encrypt_WhenCalled_GeneratesDifferentNonce()
    {
        var cryptography = CreateCryptography();

        var first = cryptography.Encrypt("123456");
        var second = cryptography.Encrypt("123456");

        Assert.NotEqual(first.Nonce, second.Nonce);
    }

    [Fact]
    public void Decrypt_WhenAuthenticationTagIsModified_ThrowsCryptographicException()
    {
        var cryptography = CreateCryptography();

        var encrypted = cryptography.Encrypt("123456");

        var tag = Convert.FromBase64String(encrypted.Tag);
        tag[0] ^= 0x01;

        var modifiedTag = Convert.ToBase64String(tag);

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            cryptography.Decrypt(
                encrypted.Ciphertext,
                encrypted.Nonce,
                modifiedTag));
    }

    [Fact]
    public void Decrypt_WhenCiphertextIsModified_ThrowsCryptographicException()
    {
        var cryptography = CreateCryptography();

        var encrypted = cryptography.Encrypt("123456");

        var ciphertext = Convert.FromBase64String(
            encrypted.Ciphertext);

        ciphertext[0] ^= 0x01;

        var modifiedCiphertext = Convert.ToBase64String(ciphertext);

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            cryptography.Decrypt(
                modifiedCiphertext,
                encrypted.Nonce,
                encrypted.Tag));
    }
}
