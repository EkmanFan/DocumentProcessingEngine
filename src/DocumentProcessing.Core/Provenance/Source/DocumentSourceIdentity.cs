using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Cryptographic identity and descriptive source metadata for one processed
/// document.
///
/// The source SHA-256 is the root of the document custody chain.
/// </summary>
public sealed record DocumentSourceIdentity
{
    public DocumentSourceIdentity(
        DocumentFormatId format,
        string sha256,
        long byteLength,
        int physicalPageCount,
        string? fileName = null,
        string? declaredMediaType = null)
    {
        if (string.IsNullOrWhiteSpace(
                format.Value))
        {
            throw new ArgumentException(
                "Document format identifier cannot be empty.",
                nameof(format));
        }

        if (byteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteLength),
                byteLength,
                "Source byte length must be greater than zero.");
        }

        if (physicalPageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageCount),
                physicalPageCount,
                "Physical page count must be greater than zero.");
        }

        Format = format;

        Sha256 =
            NormalizeSha256(
                sha256,
                nameof(sha256));

        ByteLength = byteLength;
        PhysicalPageCount = physicalPageCount;

        FileName =
            string.IsNullOrWhiteSpace(
                fileName)
                ? null
                : fileName.Trim();

        DeclaredMediaType =
            string.IsNullOrWhiteSpace(
                declaredMediaType)
                ? null
                : declaredMediaType.Trim();
    }

    public DocumentFormatId Format { get; }

    public string Sha256 { get; }

    public long ByteLength { get; }

    public int PhysicalPageCount { get; }

    public string? FileName { get; }

    public string? DeclaredMediaType { get; }

    private static string NormalizeSha256(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "SHA-256 value cannot be empty.",
                parameterName);
        }

        var normalized =
            value.Trim()
                .ToLowerInvariant();

        if (normalized.Length != 64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "SHA-256 value must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        return normalized;
    }
}
