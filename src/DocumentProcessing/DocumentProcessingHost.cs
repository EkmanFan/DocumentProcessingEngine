using DocumentProcessing.Composition;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Formats;

namespace DocumentProcessing;

/// <summary>
/// Consumer-facing, format-neutral document-processing facade.
/// </summary>
/// <remarks>
/// One Host-lifetime shared-capability composition owns selected reusable
/// Layout/OCR infrastructure. One Host-lifetime resolver owns explicit V1
/// format registration and selection.
///
/// Unsupported formats are returned as functional failures with a message.
/// Technical failures and cancellation remain exceptional.
/// </remarks>
public sealed class DocumentProcessingHost
    : IDisposable
{
    #region Variables and Constants

    private readonly SharedProcessingCapabilities
        _sharedProcessingCapabilities;

    private readonly DocumentFormatProcessorResolver
        _formatProcessorResolver;

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
            _formatProcessorResolver =
                new DocumentFormatProcessorResolver(
                    options,
                    _sharedProcessingCapabilities);
        }
        catch
        {
            _sharedProcessingCapabilities.Dispose();

            throw;
        }

        _engine =
            new DocumentProcessingEngine();
    }

    #endregion

    #region Methods Processing

    public async Task<DocumentProcessingOutcome> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        var processor =
            await _formatProcessorResolver
                .ResolveAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        if (processor is null)
        {
            return DocumentProcessingOutcome.Failure(
                "The document format is not supported.");
        }

        var result =
            await _engine
                .ProcessDocumentAsync(
                    source,
                    processor,
                    cancellationToken)
                .ConfigureAwait(false);

        return DocumentProcessingOutcome.Success(
            result);
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
