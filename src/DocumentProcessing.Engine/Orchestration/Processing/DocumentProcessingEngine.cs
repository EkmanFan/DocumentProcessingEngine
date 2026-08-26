using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Results;
using DocumentProcessing.Engine.Hybrid;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Universal document-processing orchestration boundary.
/// </summary>
/// <remarks>
/// The Engine owns format selection, native-evidence acquisition and processing
/// strategy composition.
/// </remarks>
public sealed class DocumentProcessingEngine
{
    #region Variables and Constants

    private readonly DocumentFormatSelector? _formatSelector;
    private readonly IPageLayoutAnalyzer? _layoutAnalyzer;
    private readonly IRegionTextRecognizer? _textRecognizer;
    private readonly string? _engineVersion;
    private readonly ProcessingComponentIdentity?
        _layoutAnalysisIdentity;
    private readonly UserVisualAssetWriter?
        _userVisualAssetWriter;

    #endregion

    #region ctor

    /// <summary>
    /// Creates an Engine that owns document-format selection and processing
    /// strategy composition.
    /// </summary>
    public DocumentProcessingEngine(
        IEnumerable<IDocumentFormat> formats,
        IPageLayoutAnalyzer layoutAnalyzer,
        IRegionTextRecognizer textRecognizer,
        string engineVersion,
        ProcessingComponentIdentity layoutAnalysisIdentity,
        UserVisualAssetWriter?
            userVisualAssetWriter = null)
    {
        _formatSelector =
            new DocumentFormatSelector(
                formats);

        _layoutAnalyzer =
            layoutAnalyzer ??
            throw new ArgumentNullException(
                nameof(layoutAnalyzer));

        _textRecognizer =
            textRecognizer ??
            throw new ArgumentNullException(
                nameof(textRecognizer));

        if (string.IsNullOrWhiteSpace(
                engineVersion))
        {
            throw new ArgumentException(
                "Engine version cannot be empty.",
                nameof(engineVersion));
        }

        _engineVersion =
            engineVersion.Trim();

        _layoutAnalysisIdentity =
            layoutAnalysisIdentity ??
            throw new ArgumentNullException(
                nameof(layoutAnalysisIdentity));

        _userVisualAssetWriter =
            userVisualAssetWriter;
    }

    #endregion

    #region Methods Public Processing

    /// <summary>
    /// Processes one source through the Engine-owned universal entry path.
    /// </summary>
    public Task<DocumentProcessingResult> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default) =>
        ProcessDocumentAsync(
            source,
            DocumentProcessingRequestOptions.Default,
            cancellationToken);

    /// <summary>
    /// Processes one source with explicit user-selected request options.
    /// </summary>
    public async Task<DocumentProcessingResult> ProcessDocumentAsync(
        DocumentSource source,
        DocumentProcessingRequestOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            options);

        cancellationToken.ThrowIfCancellationRequested();

        var formatSelector =
            _formatSelector ??
            throw new InvalidOperationException(
                "This document processing engine was not configured with document formats.");

        var layoutAnalyzer =
            _layoutAnalyzer ??
            throw new InvalidOperationException(
                "This document processing engine was not configured with layout analysis.");

        var textRecognizer =
            _textRecognizer ??
            throw new InvalidOperationException(
                "This document processing engine was not configured with OCR.");

        var engineVersion =
            _engineVersion ??
            throw new InvalidOperationException(
                "This document processing engine was not configured with an engine version.");

        var layoutAnalysisIdentity =
            _layoutAnalysisIdentity ??
            throw new InvalidOperationException(
                "This document processing engine was not configured with layout-analysis provenance.");

        await using var prepared =
            await PreparedDocumentSource
                .CreateAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        var selection =
            await formatSelector
                .SelectAsync(
                    prepared,
                    cancellationToken)
                .ConfigureAwait(false);

        switch (selection)
        {
            case DocumentFormatSelectionResult.NotRecognized:
                throw new DocumentFormatSelectionException(
                    "The document format is not supported.");

            case DocumentFormatSelectionResult.Invalid invalid:
                throw new DocumentFormatSelectionException(
                    invalid.IsConsumerSafeReason
                        ? invalid.Reason
                        : $"Document format '{invalid.DocumentFormat.Format}' recognized the source but rejected it: {invalid.Reason}");

            case DocumentFormatSelectionResult.Unavailable unavailable:
                throw new DocumentFormatSelectionException(
                    unavailable.Reason);

            case DocumentFormatSelectionResult.Ambiguous ambiguous:
                throw new DocumentFormatSelectionException(
                    "Document format selection is ambiguous between: " +
                    string.Join(
                        ", ",
                        ambiguous.Formats
                            .Select(
                                format =>
                                    format.Value)));

            case DocumentFormatSelectionResult.Success success
                when success.Evidence is PagedNativeDocumentEvidence:
                return await ProcessSelectedFormatAsync(
                        prepared,
                        success,
                        layoutAnalyzer,
                        textRecognizer,
                        engineVersion,
                        layoutAnalysisIdentity,
                        cancellationToken)
                    .ConfigureAwait(false);

            case DocumentFormatSelectionResult.Success success
                when success.Evidence is
                    StructuredNativeDocumentEvidence structuredEvidence:
                return await StructuredNativeDocumentProjector
                    .ProjectAsync(
                        prepared,
                        success.DocumentFormat,
                        structuredEvidence,
                        engineVersion,
                        _userVisualAssetWriter,
                        layoutAnalyzer,
                        layoutAnalysisIdentity,
                        options.QualifyUnresolvedVisuals,
                        cancellationToken)
                    .ConfigureAwait(false);

            default:
                throw new InvalidDataException(
                    $"Unsupported document format selection outcome '{selection.GetType().FullName}'.");
        }
    }

    #endregion

    #region Methods Selected Format Processing

    private async Task<DocumentProcessingResult>
        ProcessSelectedFormatAsync(
            PreparedDocumentSource prepared,
            DocumentFormatSelectionResult.Success selection,
            IPageLayoutAnalyzer layoutAnalyzer,
            IRegionTextRecognizer textRecognizer,
            string engineVersion,
            ProcessingComponentIdentity layoutAnalysisIdentity,
            CancellationToken cancellationToken)
    {
        var pagedEvidence =
            selection.Evidence as PagedNativeDocumentEvidence ??
            throw new InvalidDataException(
                $"Document format '{selection.DocumentFormat.Format}' returned " +
                "non-paged native evidence to the paged processing path.");

        var nativeExtractionIdentity =
            pagedEvidence.NativeExtractionIdentity ??
            throw new InvalidDataException(
                $"Document format '{selection.DocumentFormat.Format}' returned native evidence without acquisition provenance.");

        var documentRasterizer =
            selection.DocumentFormat as
                IDocumentRasterizer;

        var visualRasterObservationSource =
            selection.DocumentFormat as
                IVisualRasterObservationSource;

        if ((documentRasterizer is null) !=
            (visualRasterObservationSource is null))
        {
            throw new InvalidOperationException(
                $"Document format '{selection.DocumentFormat.Format}' exposes an incomplete " +
                "paged/raster enrichment capability set. The current Engine strategy " +
                "requires rasterization and native visual observation together.");
        }

        var processor =
            documentRasterizer is null
                ? DocumentProcessorFactory
                    .CreatePreparedNative(
                        selection.DocumentFormat.Format,
                        engineVersion,
                        nativeExtractionIdentity)
                : DocumentProcessorFactory
                    .CreatePreparedHybrid(
                        selection.DocumentFormat.Format,
                        documentRasterizer,
                        visualRasterObservationSource!,
                        layoutAnalyzer,
                        textRecognizer,
                        engineVersion,
                        nativeExtractionIdentity,
                        layoutAnalysisIdentity);

        Func<LayoutObservation, CancellationToken, ValueTask<Stream>>?
            openVisualDestinationAsync =
                _userVisualAssetWriter is null
                    ? null
                    : (visual, token) =>
                        _userVisualAssetWriter(
                            prepared.Source,
                            new UserLayoutVisualAssetWriteRequest(
                                selection.DocumentFormat.Format,
                                visual,
                                SourceBackedLayoutVisualMatcher
                                    .GetQualification(
                                        visual)),
                            token);

        return await processor
            .ProcessPreparedEvidencePortableAsync(
                prepared,
                selection.DocumentFormat.Format,
                pagedEvidence,
                openVisualDestinationAsync,
                cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion
}
