using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Core.Provenance;

/// <summary>
/// Optional raster-derivation evidence for one preserved visual asset.
/// </summary>
/// <remarks>
/// Some document formats, notably the current authoritative PDF path, preserve
/// a visual by rendering a source raster and extracting a deterministic crop.
/// Other formats may preserve an embedded visual directly and therefore do not
/// need this evidence.
///
/// Keeping raster derivation optional prevents the portable visual contract from
/// requiring rasterization, physical pages, or PDF-specific geometry.
/// </remarks>
public sealed record DocumentRasterVisualDerivationEvidence
{
    #region Properties

    /// <summary>
    /// Gets the source-raster width in pixels.
    /// </summary>
    public int SourcePixelWidth { get; }

    /// <summary>
    /// Gets the source-raster height in pixels.
    /// </summary>
    public int SourcePixelHeight { get; }

    /// <summary>
    /// Gets the exact pixel crop extracted from the source raster.
    /// </summary>
    public PixelRectangle Crop { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates evidence describing the raster from which a visual was derived.
    /// </summary>
    /// <param name="sourcePixelWidth">
    /// Width of the source raster in pixels.
    /// </param>
    /// <param name="sourcePixelHeight">
    /// Height of the source raster in pixels.
    /// </param>
    /// <param name="crop">
    /// Exact pixel crop extracted from the source raster.
    /// </param>
    public DocumentRasterVisualDerivationEvidence(
        int sourcePixelWidth,
        int sourcePixelHeight,
        PixelRectangle crop)
    {
        if (sourcePixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourcePixelWidth));
        }

        if (sourcePixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourcePixelHeight));
        }

        if (crop.Right > sourcePixelWidth ||
            crop.Bottom > sourcePixelHeight)
        {
            throw new ArgumentException(
                "Visual crop must remain inside the source raster.",
                nameof(crop));
        }

        SourcePixelWidth =
            sourcePixelWidth;

        SourcePixelHeight =
            sourcePixelHeight;

        Crop =
            crop;
    }

    #endregion
}
