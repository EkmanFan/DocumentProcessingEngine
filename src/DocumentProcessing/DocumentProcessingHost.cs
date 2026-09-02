using DocumentProcessing.Shared;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Epub;
using DocumentProcessing.Pdf;
using DocumentProcessing.Layout.Adapters.PpStructureV3;
using DocumentProcessing.Ocr.Adapters.PaddleOCR;

namespace DocumentProcessing;

/// <summary>
/// Consumer-facing, format-neutral document-processing facade.
/// </summary>
/// <remarks>
/// The Host owns lifecycle and composition only: shared processing
/// infrastructure, explicit document-format registration, and the configured
/// Engine. Document-format selection and processing decisions belong to the
/// Engine.
///
/// Unsupported formats remain consumer-facing functional failures. Technical
/// failures and cancellation remain exceptional.
/// </remarks>
public sealed class DocumentProcessingHost
    : IDisposable
{
    #region Variables and Constants

    private readonly SharedProcessingCapabilities
        _sharedProcessingCapabilities;

    private readonly DocumentProcessingEngine _engine;

    private readonly PhysicalPagePreviewEngine _physicalPagePreviewEngine;

    private readonly NativeDocumentNavigationEngine
        _nativeDocumentNavigationEngine;

    private bool _disposed;

    #endregion

    #region ctor

    public DocumentProcessingHost(
        DocumentProcessingHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        _sharedProcessingCapabilities =
            new SharedProcessingCapabilities(
                options.PpStructureV3,
                options.PaddleOcr,
                options.ProviderLifecycle,
                options.LoggerFactory);

        try
        {
            var formats =
                new IDocumentFormat[]
                {
                    new PdfDocumentFormat(),
                    new EpubDocumentFormat(options.Epub, options.LoggerFactory)
                };

            _engine =
                new DocumentProcessingEngine(
                    formats,
                    _sharedProcessingCapabilities.LayoutAnalyzer,
                    _sharedProcessingCapabilities.TextRecognizer,
                    options.EngineVersion,
                    _sharedProcessingCapabilities.LayoutAnalysisIdentity,
                    options.UserVisualAssetWriter);

            _physicalPagePreviewEngine =
                new PhysicalPagePreviewEngine(formats);

            _nativeDocumentNavigationEngine =
                new NativeDocumentNavigationEngine(
                    formats);
        }
        catch
        {
            _sharedProcessingCapabilities.Dispose();

            throw;
        }
    }

    #endregion

    #region Methods Preview

    /// <summary>Inspects a source without running document processing.</summary>
    public ValueTask<PhysicalPagePreviewInspection> InspectPhysicalPagesAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _physicalPagePreviewEngine.InspectAsync(source, cancellationToken);
    }

    /// <summary>Renders one physical source page as a PNG preview.</summary>
    public ValueTask RenderPhysicalPagePreviewAsync(
        DocumentSource source,
        int physicalPageNumber,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _physicalPagePreviewEngine.RenderAsync(
            source,
            physicalPageNumber,
            destination,
            cancellationToken);
    }

    /// <summary>
    /// Inspects publisher-supplied navigation without running document
    /// processing.
    /// </summary>
    public ValueTask<NativeDocumentNavigationInspection?>
        TryInspectNativeNavigationAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return _nativeDocumentNavigationEngine
            .TryInspectAsync(
                source,
                cancellationToken);
    }

    #endregion

    #region Methods Processing

    public Task<DocumentProcessingOutcome> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default) =>
        ProcessDocumentAsync(
            source,
            DocumentProcessingRequestOptions.Default,
            cancellationToken);

    /// <summary>
    /// Processes one document with explicit user-selected request options.
    /// </summary>
    public async Task<DocumentProcessingOutcome> ProcessDocumentAsync(
        DocumentSource source,
        DocumentProcessingRequestOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            options);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var result =
                await _engine
                    .ProcessDocumentAsync(
                        source,
                        options,
                        cancellationToken)
                    .ConfigureAwait(false);

            return DocumentProcessingOutcome.Success(
                result);
        }
        catch (DocumentFormatSelectionException exception)
        {
            return DocumentProcessingOutcome.Failure(
                exception.Message);
        }
    }

    #endregion

    #region Methods Lifecycle

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        _sharedProcessingCapabilities.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    #endregion
}
