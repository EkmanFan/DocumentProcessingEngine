namespace DocumentProcessing.Core.Raster;

/// <summary>
/// Neutral integrity/geometry evidence for one materialized raster output.
///
/// Binary content is deliberately not embedded. The caller owns the destination
/// stream and therefore controls whether the bytes remain temporary, are sent
/// to layout/OCR, or are persisted as a visual asset.
/// </summary>
public sealed record RasterRenderResult
{
    public RasterRenderResult(
        int physicalPageNumber,
        int sourcePagePixelWidth,
        int sourcePagePixelHeight,
        PixelRectangle? crop,
        int outputPixelWidth,
        int outputPixelHeight,
        string mediaType,
        string profileId,
        long contentLength,
        string contentSha256)
    {
        if (physicalPageNumber <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (sourcePagePixelWidth <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourcePagePixelWidth));
        }

        if (sourcePagePixelHeight <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourcePagePixelHeight));
        }

        if (outputPixelWidth <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputPixelWidth));
        }

        if (outputPixelHeight <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputPixelHeight));
        }

        if (string.IsNullOrWhiteSpace(
                mediaType) ||
            !mediaType.Trim()
                .StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Raster media type must be an image media type.",
                nameof(mediaType));
        }

        if (string.IsNullOrWhiteSpace(
                profileId))
        {
            throw new ArgumentException(
                "Raster profile ID cannot be empty.",
                nameof(profileId));
        }

        if (contentLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentLength),
                contentLength,
                "Raster content length must be greater than zero.");
        }

        var normalizedSha =
            NormalizeSha256(
                contentSha256);

        if (crop is { } region)
        {
            if (region.Right >
                    sourcePagePixelWidth ||
                region.Bottom >
                    sourcePagePixelHeight)
            {
                throw new ArgumentException(
                    "Raster crop must remain inside the source page raster.",
                    nameof(crop));
            }

            if (outputPixelWidth !=
                    region.Width ||
                outputPixelHeight !=
                    region.Height)
            {
                throw new ArgumentException(
                    "Region raster output dimensions must exactly match the requested crop.",
                    nameof(crop));
            }
        }
        else if (outputPixelWidth !=
                     sourcePagePixelWidth ||
                 outputPixelHeight !=
                     sourcePagePixelHeight)
        {
            throw new ArgumentException(
                "Full-page raster output dimensions must match the source page dimensions.");
        }

        PhysicalPageNumber =
            physicalPageNumber;

        SourcePagePixelWidth =
            sourcePagePixelWidth;

        SourcePagePixelHeight =
            sourcePagePixelHeight;

        Crop =
            crop;

        OutputPixelWidth =
            outputPixelWidth;

        OutputPixelHeight =
            outputPixelHeight;

        MediaType =
            mediaType.Trim()
                .ToLowerInvariant();

        ProfileId =
            profileId.Trim();

        ContentLength =
            contentLength;

        ContentSha256 =
            normalizedSha;
    }

    public int PhysicalPageNumber { get; }

    public int SourcePagePixelWidth { get; }

    public int SourcePagePixelHeight { get; }

    public PixelRectangle? Crop { get; }

    public int OutputPixelWidth { get; }

    public int OutputPixelHeight { get; }

    public string MediaType { get; }

    public string ProfileId { get; }

    public long ContentLength { get; }

    public string ContentSha256 { get; }

    public bool IsFullPage =>
        Crop is null;

    private static string NormalizeSha256(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Raster content SHA-256 cannot be empty.",
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
                "Raster content SHA-256 must contain exactly 64 hexadecimal characters.",
                nameof(value));
        }

        return normalized;
    }
}
