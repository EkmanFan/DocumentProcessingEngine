using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Generic document-processing dispatcher.
/// </summary>
/// <remarks>
/// This type is the format-neutral orchestration seam of the engine. It
/// identifies the input format and delegates the actual processing to the
/// single injected <see cref="IDocumentFormatProcessor"/> registered for that
/// format.
///
/// The dispatcher deliberately knows no PDF, EPUB, DOCX, layout, OCR, raster,
/// or provider-specific implementation type. Concrete format strategies are
/// supplied through constructor injection by the application's composition
/// root.
/// </remarks>
public sealed class DocumentProcessingEngine
{
    #region Variables and Constants

    private readonly IDocumentTypeDetector _documentTypeDetector;
    private readonly IReadOnlyDictionary<DocumentFormatId, IDocumentFormatProcessor>
        _formatProcessors;

    #endregion

    #region ctor

    /// <summary>
    /// Creates a format-neutral processing engine from explicitly injected
    /// format strategies.
    /// </summary>
    /// <param name="documentTypeDetector">
    /// Detector responsible for identifying the source document format.
    /// </param>
    /// <param name="formatProcessors">
    /// Concrete strategies available to process detected formats.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when a required dependency is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the strategy collection contains null entries or more than
    /// one strategy for the same format.
    /// </exception>
    public DocumentProcessingEngine(
        IDocumentTypeDetector documentTypeDetector,
        IEnumerable<IDocumentFormatProcessor> formatProcessors)
    {
        _documentTypeDetector =
            documentTypeDetector ??
            throw new ArgumentNullException(
                nameof(documentTypeDetector));

        ArgumentNullException.ThrowIfNull(
            formatProcessors);

        var processors =
            formatProcessors.ToArray();

        if (processors.Any(
                processor =>
                    processor is null))
        {
            throw new ArgumentException(
                "Document format processors cannot contain null values.",
                nameof(formatProcessors));
        }

        var duplicate =
            processors
                .GroupBy(
                    processor =>
                        processor.Format)
                .FirstOrDefault(
                    group =>
                        group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Only one document format processor can be registered for format '{duplicate.Key}'.",
                nameof(formatProcessors));
        }

        _formatProcessors =
            processors.ToDictionary(
                processor =>
                    processor.Format);
    }

    #endregion

    #region Methods Public Processing

    /// <summary>
    /// Detects the document format and delegates processing to the matching
    /// injected strategy.
    /// </summary>
    /// <param name="source">Source document to process.</param>
    /// <param name="cancellationToken">
    /// Token used to cancel detection and processing.
    /// </param>
    /// <returns>
    /// The result produced by the selected format strategy.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the format cannot be identified as supported or when no
    /// strategy has been registered for the detected format.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when detection reports a supported document without a format or a
    /// format strategy violates the non-null result contract.
    /// </exception>
    public async Task<DocumentIngestionResult> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
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
                "The document format is not supported by the configured document-processing engine.");
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
                $"No document format processor is registered for format '{format}'.");
        }

        var result =
            await processor
                .ProcessDocumentAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        return result ??
               throw new InvalidDataException(
                   $"The document format processor for '{format}' returned no result.");
    }

    #endregion
}
