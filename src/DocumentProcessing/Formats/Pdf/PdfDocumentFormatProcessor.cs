using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Planning;
using DocumentProcessing.Engine.Visual;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.Formats.Pdf;

/// <summary>
/// Transitional PDF implementation of the generic document-format strategy.
/// </summary>
/// <remarks>
/// B2.3A lets the Host construct this strategy once and select it per document.
/// The current authoritative PDF-shaped <see cref="DocumentProcessor"/> remains
/// behind the strategy until later B2 ownership/splitting work.
///
/// The current inner processor still performs its own PDF type detection. That
/// duplicate detection is explicitly transitional.
///
/// PP-StructureV3/PaddleOCR provider decoupling is not part of this change.
/// </remarks>
public sealed class PdfDocumentFormatProcessor
    : IDocumentFormatProcessor
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

    private readonly DocumentProcessor _documentProcessor;
    private readonly PdfPreservedVisualDestinationFactory?
        _openPreservedVisualDestinationAsync;

    #endregion

    #region ctor

    public PdfDocumentFormatProcessor(
        DocumentProcessor documentProcessor,
        PdfPreservedVisualDestinationFactory?
            openPreservedVisualDestinationAsync = null)
    {
        _documentProcessor =
            documentProcessor ??
            throw new ArgumentNullException(
                nameof(documentProcessor));

        _openPreservedVisualDestinationAsync =
            openPreservedVisualDestinationAsync;
    }

    #endregion

    #region Properties

    public DocumentFormatId Format =>
        DocumentFormatId.Pdf;

    #endregion

    #region Methods Composition

    internal static PdfDocumentFormatProcessor CreateForHost(
        IDocumentTypeDetector documentTypeDetector,
        PdfDocumentProcessingOptions options,
        string engineVersion,
        HttpClient layoutHttpClient,
        HttpClient ocrHttpClient)
    {
        ArgumentNullException.ThrowIfNull(
            documentTypeDetector);

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
                documentTypeDetector,
                new PdfPigDocumentExtractor(),
                new PdfPreflightAnalyzer(),
                DocumentPageProcessingPlanner.CreateDefault(),
                hybridExecution,
                engineVersion,
                NativeIdentity);

        return new PdfDocumentFormatProcessor(
            authoritativeProcessor,
            options.OpenPreservedVisualDestinationAsync);
    }

    #endregion

    #region Methods Processing

    public async Task<DocumentProcessingResult> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        var legacyResult =
            _openPreservedVisualDestinationAsync is null
                ? await _documentProcessor
                    .ProcessAsync(
                        source,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await _documentProcessor
                    .ProcessAsync(
                        source,
                        (visual, token) =>
                            _openPreservedVisualDestinationAsync(
                                source,
                                visual,
                                token),
                        cancellationToken)
                    .ConfigureAwait(false);

        return PdfDocumentProcessingResultAdapter.Adapt(
            legacyResult);
    }

    #endregion
}
