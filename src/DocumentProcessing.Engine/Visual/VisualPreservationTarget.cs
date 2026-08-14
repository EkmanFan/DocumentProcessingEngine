using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Engine.Visual;

/// <summary>
/// One layout region that deterministic policy requires the engine to preserve
/// as visual evidence, together with its exact page-raster crop.
/// </summary>
public sealed record VisualPreservationTarget(
    LayoutObservation SourceLayoutObservation,
    PixelRectangle Crop);
