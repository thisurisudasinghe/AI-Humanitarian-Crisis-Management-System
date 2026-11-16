
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Cms.Infrastructure.Security;

public class EmailProtectionService
{
    private readonly byte[] _encryptionKey;
    private readonly byte[] _hashKey;

    public EmailProtectionService(IConfiguration configuration)
    {
        var encryptionKey = configuration["EmailProtection:EncryptionKey"];
        var hashKey = configuration["EmailProtection:HashKey"];

        if (string.IsNullOrWhiteSpace(encryptionKey) ||
            string.IsNullOrWhiteSpace(hashKey))
        {
            throw new InvalidOperationException(
                "Email protection keys are missing from configuration.");
        }

        try
        {
            _encryptionKey = Convert.FromBase64String(encryptionKey);
            _hashKey = Convert.FromBase64String(hashKey);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Email protection keys must be valid Base64 values.");
        }

        if (_encryptionKey.Length != 32 || _hashKey.Length != 32)
        {
            throw new InvalidOperationException(
                "Email protection keys must each contain exactly 32 bytes.");
        }
    }

    public string EncryptEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var plaintext = Encoding.UTF8.GetBytes(normalizedEmail);

        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using (var aes = new AesGcm(_encryptionKey, tag.Length))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        var combined = new byte[nonce.Length + tag.Length + ciphertext.Length];

        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, combined,
            nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(combined);
    }

    public string DecryptEmail(string encryptedEmail)
    {
        var combined = Convert.FromBase64String(encryptedEmail);

        if (combined.Length < 28)
        {
            throw new CryptographicException(
                "Encrypted email data is invalid.");
        }

        var nonce = combined[..12];
        var tag = combined[12..28];
        var ciphertext = combined[28..];
        var plaintext = new byte[ciphertext.Length];

        using (var aes = new AesGcm(_encryptionKey, tag.Length))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    public string HashEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var emailBytes = Encoding.UTF8.GetBytes(normalizedEmail);

        using var hmac = new HMACSHA256(_hashKey);

        return Convert.ToHexString(hmac.ComputeHash(emailBytes));
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException(
                "Email cannot be empty.", nameof(email));
        }

        return email.Trim().ToLowerInvariant();
    }
}
