using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Formats.Pdf;

namespace DocumentProcessing.Formats;

/// <summary>
/// Host-lifetime registry and resolver for document-format processors.
/// </summary>
/// <remarks>
/// V1 deliberately uses explicit hard-coded registration. No assembly scanning,
/// reflection-based discovery, or hot loading is performed.
///
/// The resolver knows only format processors. Each processor encapsulates how
/// it validates candidate input for its own format.
/// </remarks>
internal sealed class DocumentFormatProcessorResolver
    : IDisposable
{
    #region Variables and Constants

    private readonly IReadOnlyDictionary<DocumentFormatId, IDocumentFormatProcessor>
        _formatProcessors;

    private readonly HttpClient _layoutHttpClient;
    private readonly HttpClient _ocrHttpClient;

    private bool _disposed;

    #endregion

    #region ctor

    public DocumentFormatProcessorResolver(
        DocumentProcessingHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        _layoutHttpClient =
            CreateServiceHttpClient();

        _ocrHttpClient =
            CreateServiceHttpClient();

        try
        {
            var pdfProcessor =
                PdfDocumentFormatProcessor.CreateForHost(
                    options.Pdf,
                    options.EngineVersion,
                    _layoutHttpClient,
                    _ocrHttpClient);

            _formatProcessors =
                new Dictionary<DocumentFormatId, IDocumentFormatProcessor>
                {
                    [pdfProcessor.Format] =
                        pdfProcessor
                };
        }
        catch
        {
            _layoutHttpClient.Dispose();
            _ocrHttpClient.Dispose();

            throw;
        }
    }

    #endregion

    #region Methods Resolution

    public async ValueTask<IDocumentFormatProcessor?> ResolveAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var processor in
                 _formatProcessors.Values)
        {
            if (await processor
                    .ValidateAsync(
                        source,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                return processor;
            }
        }

        return null;
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

        _layoutHttpClient.Dispose();
        _ocrHttpClient.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private static HttpClient CreateServiceHttpClient() =>
        new()
        {
            Timeout =
                Timeout.InfiniteTimeSpan
        };

    #endregion
}
