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
/// PDF implementation of the generic document-format strategy.
/// </summary>
/// <remarks>
/// The strategy owns PDF format validation and the current authoritative PDF
/// processing composition. Generic routing asks this processor whether it can
/// handle a source and never sees the PDF validator directly.
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

    private readonly IFormatValidator _validator;
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
        _validator =
            new PdfFormatValidator();

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
            authoritativeProcessor,
            options.OpenPreservedVisualDestinationAsync);
    }

    #endregion

    #region Methods Validation

    public ValueTask<bool> ValidateAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        return _validator.ValidateAsync(
            source,
            cancellationToken);
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
