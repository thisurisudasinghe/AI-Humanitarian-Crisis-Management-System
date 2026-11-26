using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Cms.Infrastructure.Access;

/// <summary>
/// Shared helpers for the access/reporting workflows. References are opaque,
/// collision-resistant public identifiers; tracking tokens are high-entropy
/// UUIDs whose SHA-256 hash is the only value persisted.
/// </summary>
public static class AccessKeys
{
    public static string NewReference(string prefix)
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var random = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        return $"{prefix}-{date}-{random}";
    }

    public static bool IsValidTrackingToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) && Guid.TryParseExact(token.Trim(), "D", out _);

    public static string NormalizeTrackingToken(string token) =>
        Guid.ParseExact(token.Trim(), "D").ToString("D");

    public static string HashTrackingToken(string normalizedToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedToken)));
}
