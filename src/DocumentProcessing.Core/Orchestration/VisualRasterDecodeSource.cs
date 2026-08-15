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
    /// The PDF image's raw embedded bytes were directly decodable.
    /// </summary>
    RawEmbeddedImage,

    /// <summary>
    /// PdfPig converted the PDF image to PNG before RGBA decoding.
    /// </summary>
    PdfPigPng
}
