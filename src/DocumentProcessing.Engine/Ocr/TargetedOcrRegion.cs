using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Engine.Ocr;

/// <summary>
/// A layout region that deterministic policy has authorized for OCR, together
/// with the exact pixel crop required from the page raster.
/// </summary>
public sealed record TargetedOcrRegion(
    LayoutObservation SourceLayoutObservation,
    RasterCropRectangle Crop);
