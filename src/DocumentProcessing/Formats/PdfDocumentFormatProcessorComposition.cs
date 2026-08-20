using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.Formats;

/// <summary>
/// Composes the current PDF format adapter from PDF-specific capabilities and
/// shared processing capabilities.
/// </summary>
/// <remarks>
/// Engine-internal planner, hybrid-executor, visual-preservation and
/// reconciliation composition is owned by <see cref="DocumentProcessorFactory"/>
/// and is deliberately not reproduced here.
/// </remarks>
internal static class PdfDocumentFormatProcessorComposition
{
    #region Variables and Constants

    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "pdfpig",
            "pdfpig-native-v1");

    private static readonly ProcessingComponentIdentity LayoutIdentity =
        new(
            "pp-structurev3",
            "pp-structurev3-3.7.0-paddle3.2.2-cpu-v1");

    #endregion

    #region Methods Composition

    public static PdfDocumentFormatProcessor Create(
        PdfDocumentProcessingOptions options,
        string engineVersion,
        HttpClient layoutHttpClient,
        HttpClient ocrHttpClient)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        ArgumentNullException.ThrowIfNull(
            layoutHttpClient);

        ArgumentNullException.ThrowIfNull(
            ocrHttpClient);

        if (string.IsNullOrWhiteSpace(
                engineVersion))
        {
            throw new ArgumentException(
                "Engine version cannot be empty.",
                nameof(engineVersion));
        }

        var layoutAnalyzer =
            new PpStructureV3PageLayoutAnalyzer(
                new PpStructureV3ServingClient(
                    layoutHttpClient,
                    options.LayoutEndpoint,
                    options.LayoutRequestTimeout));

        var textRecognizer =
            new PaddleOcrRegionTextRecognizer(
                new PaddleOcrServingClient(
                    ocrHttpClient,
                    options.OcrEndpoint,
                    options.OcrProfileId,
                    options.OcrRequestTimeout));

        var authoritativeProcessor =
            DocumentProcessorFactory.CreateHybrid(
                DocumentFormatId.Pdf,
                new PdfPigDocumentExtractor(),
                new PdfPreflightAnalyzer(),
                new PdftoppmDocumentRasterizer(
                    dpi:
                        300),
                new PdfPigVisualRasterObservationSource(),
                layoutAnalyzer,
                textRecognizer,
                engineVersion,
                NativeIdentity,
                LayoutIdentity);

        return new PdfDocumentFormatProcessor(
            ExecuteAsync,
            options.OpenPreservedVisualDestinationAsync);

        Task<DocumentIngestionResult> ExecuteAsync(
            DocumentSource source,
            PdfPreservedVisualDestinationFactory?
                openPreservedVisualDestinationAsync,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(
                source);

            cancellationToken.ThrowIfCancellationRequested();

            return openPreservedVisualDestinationAsync is null
                ? authoritativeProcessor.ProcessAsync(
                    source,
                    cancellationToken)
                : authoritativeProcessor.ProcessAsync(
                    source,
                    (visual, token) =>
                        openPreservedVisualDestinationAsync(
                            source,
                            visual,
                            token),
                    cancellationToken);
        }
    }

    #endregion
}
