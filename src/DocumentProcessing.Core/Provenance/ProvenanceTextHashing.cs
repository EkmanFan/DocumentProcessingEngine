using System.Security.Cryptography;
using System.Text;

namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Canonical hashing rule for portable documentary text.
///
/// The exact returned string is encoded as UTF-8 bytes without BOM and hashed
/// with SHA-256. The hexadecimal representation is lowercase.
///
/// This utility is public so downstream consumers can independently verify the
/// hashes carried by the custody-complete result.
/// </summary>
public static class ProvenanceTextHashing
{
    public static string ComputeUtf8Sha256(
        string text)
    {
        ArgumentNullException.ThrowIfNull(
            text);

        var bytes =
            Encoding.UTF8.GetBytes(
                text);

        return Convert
            .ToHexString(
                SHA256.HashData(
                    bytes))
            .ToLowerInvariant();
    }

    public static bool MatchesUtf8Sha256(
        string text,
        string sha256)
    {
        ArgumentNullException.ThrowIfNull(
            text);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sha256);

        return string.Equals(
            ComputeUtf8Sha256(
                text),
            sha256.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}
