using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Formats;

namespace DocumentProcessing;

/// <summary>
/// Consumer-facing, format-neutral document-processing facade.
/// </summary>
/// <remarks>
/// The Host knows no concrete document format. One Host-lifetime resolver owns
/// V1 processor registration and asks each processor whether it can handle the
/// supplied source.
///
/// Unsupported formats are returned as functional failures with a message.
/// Technical failures and cancellation remain exceptional.
/// </remarks>
public sealed class DocumentProcessingHost
    : IDisposable
{
    #region Variables and Constants

    private readonly DocumentFormatProcessorResolver _formatProcessorResolver;
    private readonly DocumentProcessingEngine _engine;

    private bool _disposed;

    #endregion

    #region ctor

    public DocumentProcessingHost(
        DocumentProcessingHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        _formatProcessorResolver =
            new DocumentFormatProcessorResolver(
                options);

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

        _formatProcessorResolver.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    #endregion
}
