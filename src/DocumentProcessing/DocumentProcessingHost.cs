using DocumentProcessing.Shared;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Epub;
using DocumentProcessing.Pdf;
using DocumentProcessing.Layout.Adapters.PpStructureV3;

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
                options.PaddleOcr);

        try
        {
            _engine =
                new DocumentProcessingEngine(
                    [
                        new PdfDocumentFormat(),
                        new EpubDocumentFormat(
                            options.Epub,
                            options.LoggerFactory)
                    ],
                    _sharedProcessingCapabilities.LayoutAnalyzer,
                    _sharedProcessingCapabilities.TextRecognizer,
                    options.EngineVersion,
                    _sharedProcessingCapabilities.LayoutAnalysisIdentity,
                    options.UserVisualAssetWriter);
        }
        catch
        {
            _sharedProcessingCapabilities.Dispose();

            throw;
        }
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
