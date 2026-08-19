using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Formats.Pdf;
using DocumentProcessing.Pdf;

namespace DocumentProcessing;

/// <summary>
/// Consumer-facing composition root and document-format router.
/// </summary>
/// <remarks>
/// V1 uses explicit manual dependency composition. The Host creates and owns
/// format detection and one strategy instance per configured format, selects
/// the strategy for each source, then injects that selected strategy into the
/// neutral Engine call.
///
/// Consumers supply configuration values only, not processing services.
/// Future dynamic strategy discovery is deliberately outside V1.
/// </remarks>
public sealed class DocumentProcessingHost
    : IDisposable
{
    #region Variables and Constants

    private readonly IDocumentTypeDetector _documentTypeDetector;
    private readonly IReadOnlyDictionary<DocumentFormatId, IDocumentFormatProcessor>
        _formatProcessors;

    private readonly DocumentProcessingEngine _engine;
    private readonly HttpClient _layoutHttpClient;
    private readonly HttpClient _ocrHttpClient;

    private bool _disposed;

    #endregion

    #region ctor

    public DocumentProcessingHost(
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
            _documentTypeDetector =
                new PdfDocumentTypeDetector();

            var pdfProcessor =
                PdfDocumentFormatProcessor.CreateForHost(
                    _documentTypeDetector,
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

            _engine =
                new DocumentProcessingEngine();
        }
        catch
        {
            _layoutHttpClient.Dispose();
            _ocrHttpClient.Dispose();

            throw;
        }
    }

    #endregion

    #region Methods Processing

    public async Task<DocumentProcessingResult> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        var detection =
            await _documentTypeDetector
                .DetectAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!detection.IsSupported)
        {
            throw new NotSupportedException(
                "The document format is not supported by this document-processing Host.");
        }

        if (detection.Format is not { } format)
        {
            throw new InvalidDataException(
                "Document type detection reported a supported document without a format identifier.");
        }

        if (!_formatProcessors.TryGetValue(
                format,
                out var processor))
        {
            throw new NotSupportedException(
                $"No Host-owned document format strategy is configured for format '{format}'.");
        }

        return await _engine
            .ProcessDocumentAsync(
                source,
                processor,
                cancellationToken)
            .ConfigureAwait(false);
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
