using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Engine.Visual;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.Formats;

/// <summary>
/// Composes the current authoritative Engine implementation used by the PDF
/// format processor.
/// </summary>
/// <remarks>
/// This type intentionally lives in the top-level composition assembly because
/// it is the boundary allowed to know both generic Engine implementations and
/// concrete PDF capabilities.
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

    private static readonly ProcessingComponentIdentity ReconciliationIdentity =
        new(
            "native-ocr-text-reconciler",
            "native-ocr-reconciliation-v1");

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

        var visualPreserver =
            new VisualAssetPreserver();

        var hybridExecution =
            new DocumentHybridExecutionDependencies(
                new PdftoppmDocumentRasterizer(
                    dpi:
                        300),
                new MissingNativeHybridPageExecutor(
                    layoutAnalyzer,
                    textRecognizer,
                    visualPreserver),
                new NativePresentHybridPageExecutor(
                    layoutAnalyzer,
                    textRecognizer,
                    visualPreserver),
                LayoutIdentity,
                ReconciliationIdentity,
                new DocumentAuthoritativeVisualPlanningDependencies(
                    new PdfPigVisualRasterObservationSource()),
                new HealthyNativeVisualPageExecutor(
                    layoutAnalyzer,
                    visualPreserver));

        var authoritativeProcessor =
            new DocumentProcessor(
                DocumentFormatId.Pdf,
                new PdfPigDocumentExtractor(),
                new PdfPreflightAnalyzer(),
                DocumentPageProcessingPlanner.CreateDefault(),
                hybridExecution,
                engineVersion,
                NativeIdentity);

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
