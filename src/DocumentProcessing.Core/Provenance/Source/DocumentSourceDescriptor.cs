using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Format-neutral identity and descriptive metadata for the source artifact
/// processed by the engine.
/// </summary>
/// <remarks>
/// The SHA-256 is the root of source custody. This descriptor deliberately
/// contains no physical-page count or other format-specific structural state.
///
/// The existing <see cref="DocumentSourceIdentity"/> remains unchanged for the
/// current V1 PDF result while migration is in progress. The future
/// DocumentProcessingResult will use this descriptor instead.
/// </remarks>
public sealed record DocumentSourceDescriptor
{
    #region ctor

    /// <summary>
    /// Creates a portable source descriptor.
    /// </summary>
    /// <param name="format">Detected document format.</param>
    /// <param name="sha256">SHA-256 of the exact source bytes.</param>
    /// <param name="byteLength">Exact source byte length.</param>
    /// <param name="fileName">Optional caller-provided file name.</param>
    /// <param name="declaredMediaType">
    /// Optional caller-provided media type.
    /// </param>
    public DocumentSourceDescriptor(
        DocumentFormatId format,
        string sha256,
        long byteLength,
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

        Format =
            format;

        Sha256 =
            NormalizeSha256(
                sha256,
                nameof(sha256));

        ByteLength =
            byteLength;

        FileName =
            NormalizeOptional(
                fileName);

        DeclaredMediaType =
            NormalizeOptional(
                declaredMediaType);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the detected source document format.
    /// </summary>
    public DocumentFormatId Format { get; }

    /// <summary>
    /// Gets the normalized lowercase SHA-256 of the exact source bytes.
    /// </summary>
    public string Sha256 { get; }

    /// <summary>
    /// Gets the exact source byte length.
    /// </summary>
    public long ByteLength { get; }

    /// <summary>
    /// Gets the optional caller-provided file name.
    /// </summary>
    public string? FileName { get; }

    /// <summary>
    /// Gets the optional caller-provided media type.
    /// </summary>
    public string? DeclaredMediaType { get; }

    #endregion

    #region Methods Validation

    private static string? NormalizeOptional(
        string? value) =>
        string.IsNullOrWhiteSpace(
            value)
            ? null
            : value.Trim();

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

    #endregion
}
