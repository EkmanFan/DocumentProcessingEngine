namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Deterministic byte source that produced the decoded RGBA raster used for
/// low-level visual measurement.
/// </summary>
public enum VisualRasterDecodeSource
{
    /// <summary>
    /// No supported decoded raster was available.
    /// </summary>
    Unavailable,

    /// <summary>
    /// The source visual's raw embedded bytes were directly decodable.
    /// </summary>
    RawEmbeddedImage,

    /// <summary>
    /// The format implementation converted the source visual to PNG before
    /// RGBA decoding.
    /// </summary>
    ConvertedPng
}
