using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Universal document-processing orchestration boundary.
/// </summary>
/// <remarks>
/// The configured path owns format selection, native-evidence acquisition and
/// Engine strategy composition. The selected-format processor overload remains
/// temporarily as a compatibility path until the Host cutover is complete.
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
    private readonly PreservedLayoutVisualDestinationFactory?
        _openPreservedLayoutVisualDestinationAsync;

    #endregion

    #region ctor

    /// <summary>
    /// Temporary compatibility constructor for the current Host path.
    /// </summary>
    public DocumentProcessingEngine()
    {
    }

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
        PreservedLayoutVisualDestinationFactory?
            openPreservedLayoutVisualDestinationAsync = null)
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

        _openPreservedLayoutVisualDestinationAsync =
            openPreservedLayoutVisualDestinationAsync;
    }

    #endregion

    #region Methods Public Processing

    /// <summary>
    /// Processes one source through the Engine-owned universal entry path.
    /// </summary>
    public async Task<DocumentProcessingResult> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

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
                throw new NotSupportedException(
                    "No registered document format recognized the source.");

            case DocumentFormatSelectionResult.Invalid invalid:
                throw new InvalidDataException(
                    $"Document format '{invalid.DocumentFormat.Format}' recognized the source but rejected it: {invalid.Reason}");

            case DocumentFormatSelectionResult.Ambiguous ambiguous:
                throw new InvalidDataException(
                    "Document format selection is ambiguous between: " +
                    string.Join(
                        ", ",
                        ambiguous.Formats
                            .Select(
                                format =>
                                    format.Value)));

            case DocumentFormatSelectionResult.Success success:
                return await ProcessSelectedFormatAsync(
                        prepared,
                        success,
                        layoutAnalyzer,
                        textRecognizer,
                        engineVersion,
                        layoutAnalysisIdentity,
                        cancellationToken)
                    .ConfigureAwait(false);

            default:
                throw new InvalidDataException(
                    $"Unsupported document format selection outcome '{selection.GetType().FullName}'.");
        }
    }

    /// <summary>
    /// Temporary compatibility path used by the current Host until Step 2.
    /// </summary>
    public async Task<DocumentProcessingResult> ProcessDocumentAsync(
        DocumentSource source,
        IDocumentFormatProcessor formatProcessor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            formatProcessor);

        cancellationToken.ThrowIfCancellationRequested();

        var result =
            await formatProcessor
                .ProcessDocumentAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        return result ??
               throw new InvalidDataException(
                   $"The selected document format processor for '{formatProcessor.Format}' returned no result.");
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
        var nativeExtractionIdentity =
            selection.Evidence.NativeExtractionIdentity ??
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
                _openPreservedLayoutVisualDestinationAsync is null
                    ? null
                    : (visual, token) =>
                        _openPreservedLayoutVisualDestinationAsync(
                            prepared.Source,
                            visual,
                            token);

        return await processor
            .ProcessPreparedEvidencePortableAsync(
                prepared,
                selection.DocumentFormat.Format,
                selection.Evidence,
                openVisualDestinationAsync,
                cancellationToken)
            .ConfigureAwait(false);
    }

    #endregion
}
