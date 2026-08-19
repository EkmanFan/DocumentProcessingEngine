using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Core.Ocr;

/// <summary>
/// Describes one layout region that deterministic processing has authorized for
/// OCR, together with the exact pixel crop to render for recognition.
/// </summary>
/// <remarks>
/// This is a neutral processing contract, not an OCR-planning policy and not a
/// PDF implementation type. The engine decides which regions are OCR-authorized;
/// format-specific rasterizers and OCR recognizers consume the resulting
/// contract.
///
/// Keeping this value in Core allows processing behavior to exchange an OCR work
/// item without making Core depend on Engine or on a concrete document format.
/// </remarks>
public sealed record TargetedOcrRegion(
    LayoutObservation SourceLayoutObservation,
    PixelRectangle Crop);
