using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Portable integrity metadata for one preserved visual asset.
///
/// Binary bytes are deliberately not embedded here.
/// </summary>
public sealed record PreservedVisualProvenance
{
    public PreservedVisualProvenance(
        string profileId,
        string mediaType,
        int sourceRasterPixelWidth,
        int sourceRasterPixelHeight,
        PixelRectangle crop,
        long contentLength,
        string contentSha256)
    {
        if (string.IsNullOrWhiteSpace(
                profileId))
        {
            throw new ArgumentException(
                "Visual preservation profile ID cannot be empty.",
                nameof(profileId));
        }

        if (string.IsNullOrWhiteSpace(
                mediaType))
        {
            throw new ArgumentException(
                "Visual media type cannot be empty.",
                nameof(mediaType));
        }

        if (sourceRasterPixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceRasterPixelWidth));
        }

        if (sourceRasterPixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceRasterPixelHeight));
        }

        if (contentLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentLength));
        }

        ProfileId =
            profileId.Trim();

        MediaType =
            mediaType.Trim()
                .ToLowerInvariant();

        SourceRasterPixelWidth =
            sourceRasterPixelWidth;

        SourceRasterPixelHeight =
            sourceRasterPixelHeight;

        Crop = crop;
        ContentLength = contentLength;

        ContentSha256 =
            NormalizeSha256(
                contentSha256,
                nameof(contentSha256));
    }

    public string ProfileId { get; }

    public string MediaType { get; }

    public int SourceRasterPixelWidth { get; }

    public int SourceRasterPixelHeight { get; }

    public PixelRectangle Crop { get; }

    public long ContentLength { get; }

    public string ContentSha256 { get; }

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
