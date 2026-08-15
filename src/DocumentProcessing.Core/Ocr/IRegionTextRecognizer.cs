using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Core.Ocr;

/// <summary>
/// Narrow external-capability boundary for recognizing text from one
/// deterministic layout-authorized raster region.
/// </summary>
public interface IRegionTextRecognizer
{
    ValueTask<OcrRegionResult> RecognizeAsync(
        Stream rasterRegion,
        LayoutObservation sourceLayoutObservation,
        PixelRectangle crop,
        int pagePixelWidth,
        int pagePixelHeight,
        CancellationToken cancellationToken = default);
}
