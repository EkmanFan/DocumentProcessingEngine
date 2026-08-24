using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Core.Ocr;

/// <summary>
/// Narrow external-capability boundary for recognizing text from one
/// raster region already authorized by deterministic Engine policy.
/// Implementations perform recognition and technical validation only; they
/// do not own the Engine policy that decides whether OCR is appropriate.
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
