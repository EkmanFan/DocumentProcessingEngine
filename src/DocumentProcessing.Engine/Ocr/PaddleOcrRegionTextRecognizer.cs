using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Engine.Ocr;

/// <summary>
/// Testable orchestration adapter over the selected PaddleOCR serving client.
///
/// The abstraction exists because OCR is a real external service boundary. It
/// does not introduce a recognizer registry or plugin system.
/// </summary>
public sealed class PaddleOcrRegionTextRecognizer
    : IRegionTextRecognizer
{
    private readonly PaddleOcrServingClient _client;

    public PaddleOcrRegionTextRecognizer(
        PaddleOcrServingClient client)
    {
        _client =
            client ??
            throw new ArgumentNullException(
                nameof(client));
    }

    public ValueTask<OcrRegionResult> RecognizeAsync(
        Stream rasterRegion,
        LayoutObservation sourceLayoutObservation,
        PixelRectangle crop,
        int pagePixelWidth,
        int pagePixelHeight,
        CancellationToken cancellationToken = default) =>
        _client.RecognizeAsync(
            rasterRegion,
            sourceLayoutObservation,
            crop,
            pagePixelWidth,
            pagePixelHeight,
            cancellationToken);
}
