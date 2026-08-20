using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Core.Results;

/// <summary>
/// Portable integrity and custody descriptor for one preserved visual asset.
/// </summary>
/// <remarks>
/// Binary bytes are deliberately not embedded in the result. The consumer owns
/// persistence, while this descriptor identifies exactly which asset was
/// produced and links it to the document element that represents the visual.
///
/// <see cref="RasterDerivation"/> is optional. A paged/rasterized processor can
/// retain deterministic crop evidence, while a format such as EPUB can preserve
/// an embedded image directly without inventing raster or physical-page state.
/// </remarks>
public sealed record DocumentVisualAsset
{
    #region Properties

    /// <summary>
    /// Gets the stable visual-asset identifier within the result.
    /// </summary>
    public string AssetId { get; }

    /// <summary>
    /// Gets the document-element identifier owning this visual asset.
    /// </summary>
    public string ElementId { get; }

    /// <summary>
    /// Gets the profile describing how the preserved asset was produced.
    /// </summary>
    public string PreservationProfileId { get; }

    /// <summary>
    /// Gets the normalized image media type.
    /// </summary>
    public string MediaType { get; }

    /// <summary>
    /// Gets the exact preserved byte length.
    /// </summary>
    public long ContentLength { get; }

    /// <summary>
    /// Gets the normalized lowercase SHA-256 of the preserved bytes.
    /// </summary>
    public string ContentSha256 { get; }

    /// <summary>
    /// Gets optional raster/crop derivation evidence.
    /// </summary>
    public DocumentRasterVisualDerivationEvidence? RasterDerivation { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one portable preserved-visual descriptor.
    /// </summary>
    /// <param name="assetId">
    /// Stable visual-asset identifier within the processing result.
    /// </param>
    /// <param name="elementId">
    /// Identifier of the visual document element owning this asset.
    /// </param>
    /// <param name="preservationProfileId">
    /// Profile describing how the preserved asset was produced.
    /// </param>
    /// <param name="mediaType">Image media type of the preserved bytes.</param>
    /// <param name="contentLength">Exact preserved byte length.</param>
    /// <param name="contentSha256">
    /// SHA-256 of the exact preserved bytes.
    /// </param>
    /// <param name="rasterDerivation">
    /// Optional raster/crop evidence when the asset was derived from a raster.
    /// </param>
    public DocumentVisualAsset(
        string assetId,
        string elementId,
        string preservationProfileId,
        string mediaType,
        long contentLength,
        string contentSha256,
        DocumentRasterVisualDerivationEvidence? rasterDerivation = null)
    {
        if (string.IsNullOrWhiteSpace(
                assetId))
        {
            throw new ArgumentException(
                "Visual asset ID cannot be empty.",
                nameof(assetId));
        }

        if (string.IsNullOrWhiteSpace(
                elementId))
        {
            throw new ArgumentException(
                "Visual asset element ID cannot be empty.",
                nameof(elementId));
        }

        if (string.IsNullOrWhiteSpace(
                preservationProfileId))
        {
            throw new ArgumentException(
                "Visual preservation profile ID cannot be empty.",
                nameof(preservationProfileId));
        }

        if (string.IsNullOrWhiteSpace(
                mediaType) ||
            !mediaType.Trim()
                .StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Visual asset media type must be an image media type.",
                nameof(mediaType));
        }

        if (contentLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(contentLength),
                contentLength,
                "Visual asset content length must be greater than zero.");
        }

        AssetId =
            assetId.Trim();

        ElementId =
            elementId.Trim();

        PreservationProfileId =
            preservationProfileId.Trim();

        MediaType =
            mediaType.Trim()
                .ToLowerInvariant();

        ContentLength =
            contentLength;

        ContentSha256 =
            NormalizeSha256(
                contentSha256,
                nameof(contentSha256));

        RasterDerivation =
            rasterDerivation;
    }

    #endregion

    #region Methods Validation

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
