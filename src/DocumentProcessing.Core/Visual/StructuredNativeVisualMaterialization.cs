namespace DocumentProcessing.Core.Visual;

/// <summary>
/// Integrity and provenance produced while copying one exact source-native
/// visual resource.
/// </summary>
public sealed record StructuredNativeVisualMaterialization
{
    #region Properties

    public string ProfileId { get; }

    public string MediaType { get; }

    public long ContentLength { get; }

    public string ContentSha256 { get; }

    #endregion

    #region ctor

    public StructuredNativeVisualMaterialization(
        string profileId,
        string mediaType,
        long contentLength,
        string contentSha256)
    {
        if (string.IsNullOrWhiteSpace(
                profileId))
        {
            throw new ArgumentException(
                "Visual materialization profile ID cannot be empty.",
                nameof(profileId));
        }

        if (string.IsNullOrWhiteSpace(
                mediaType) ||
            !mediaType.Trim()
                .StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Visual materialization media type must be an image media type.",
                nameof(mediaType));
        }

        if (contentLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentLength));
        }

        ProfileId =
            profileId.Trim();

        MediaType =
            mediaType.Trim()
                .ToLowerInvariant();

        ContentLength =
            contentLength;

        ContentSha256 =
            NormalizeSha256(
                contentSha256);
    }

    #endregion

    #region Methods Validation

    private static string NormalizeSha256(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "SHA-256 value cannot be empty.",
                nameof(value));
        }

        var normalized =
            value.Trim()
                .ToLowerInvariant();

        if (normalized.Length !=
                64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "SHA-256 value must contain exactly 64 hexadecimal characters.",
                nameof(value));
        }

        return normalized;
    }

    #endregion
}
