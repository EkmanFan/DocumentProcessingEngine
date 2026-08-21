using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Core.Visual;

/// <summary>
/// Neutral provenance and integrity evidence for one preserved raster visual.
///
/// Binary content is deliberately not embedded in this model. The caller owns
/// the destination used to persist the bytes, while this record identifies and
/// audits exactly what was preserved.
/// </summary>
public sealed record PreservedVisualEvidence
{
    public PreservedVisualEvidence(
        string sourceDocumentSha256,
        string profileId,
        string mediaType,
        LayoutObservation sourceLayoutObservation,
        int sourceRasterPixelWidth,
        int sourceRasterPixelHeight,
        PixelRectangle crop,
        long contentLength,
        string contentSha256,
        DocumentVisualQualification qualification =
            DocumentVisualQualification.Meaningful)
    {
        ArgumentNullException.ThrowIfNull(sourceLayoutObservation);

        SourceDocumentSha256 =
            NormalizeSha256(
                sourceDocumentSha256,
                nameof(sourceDocumentSha256));

        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw new ArgumentException(
                "Visual preservation profile ID cannot be empty.",
                nameof(profileId));
        }

        if (string.IsNullOrWhiteSpace(mediaType) ||
            !mediaType.Trim().StartsWith(
                "image/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Preserved visual media type must be an image media type.",
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

        if (crop.Right > sourceRasterPixelWidth ||
            crop.Bottom > sourceRasterPixelHeight)
        {
            throw new ArgumentException(
                "Visual crop must remain inside the source raster.",
                nameof(crop));
        }

        if (contentLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentLength),
                contentLength,
                "Preserved visual content length must be greater than zero.");
        }

        if (!Enum.IsDefined(
                qualification))
        {
            throw new ArgumentOutOfRangeException(
                nameof(qualification));
        }

        ProfileId = profileId.Trim();
        MediaType = mediaType.Trim().ToLowerInvariant();
        SourceLayoutObservation = sourceLayoutObservation;
        SourceRasterPixelWidth = sourceRasterPixelWidth;
        SourceRasterPixelHeight = sourceRasterPixelHeight;
        Crop = crop;
        ContentLength = contentLength;
        ContentSha256 =
            NormalizeSha256(
                contentSha256,
                nameof(contentSha256));

        Qualification =
            qualification;
    }

    public string SourceDocumentSha256 { get; }

    public string ProfileId { get; }

    public string MediaType { get; }

    public LayoutObservation SourceLayoutObservation { get; }

    public int SourceRasterPixelWidth { get; }

    public int SourceRasterPixelHeight { get; }

    public PixelRectangle Crop { get; }

    public long ContentLength { get; }

    public string ContentSha256 { get; }

    public DocumentVisualQualification Qualification { get; }

    private static string NormalizeSha256(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
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
                    !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "SHA-256 value must contain exactly 64 hexadecimal characters.",
                parameterName);
        }

        return normalized;
    }
}
