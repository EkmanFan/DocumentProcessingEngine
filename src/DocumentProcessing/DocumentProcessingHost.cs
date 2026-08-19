using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing;

/// <summary>
/// Consumer-facing host for document processing.
/// </summary>
/// <remarks>
/// The host owns the format-neutral <see cref="DocumentProcessingEngine"/> and
/// injects the explicitly supplied document-format strategies into it.
///
/// Consumers process documents through this facade and do not resolve format
/// processors, PDF internals, OCR/layout providers, or other implementation
/// services at execution time. This is deliberate constructor injection, not a
/// Service Locator.
///
/// The current return type remains <see cref="DocumentIngestionResult"/> only
/// until the portable multi-format result contract is redesigned and renamed to
/// DocumentProcessingResult.
/// </remarks>
public sealed class DocumentProcessingHost
{
    #region Variables and Constants

    private readonly DocumentProcessingEngine _engine;

    #endregion

    #region ctor

    /// <summary>
    /// Creates a processing host from an explicit document-type detector and
    /// the available format-specific processing strategies.
    /// </summary>
    /// <param name="documentTypeDetector">
    /// Detector used by the generic engine to identify the source format.
    /// </param>
    /// <param name="formatProcessors">
    /// Format-specific strategies injected into the generic engine.
    /// </param>
    public DocumentProcessingHost(
        IDocumentTypeDetector documentTypeDetector,
        IEnumerable<IDocumentFormatProcessor> formatProcessors)
    {
        _engine =
            new DocumentProcessingEngine(
                documentTypeDetector,
                formatProcessors);
    }

    #endregion

    #region Methods Processing

    /// <summary>
    /// Processes one source document through the strategy selected for its
    /// detected format.
    /// </summary>
    /// <param name="source">Source document to process.</param>
    /// <param name="cancellationToken">
    /// Token used to cancel document processing.
    /// </param>
    /// <returns>
    /// The current portable processing result.
    /// </returns>
    public Task<DocumentIngestionResult> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default) =>
        _engine.ProcessDocumentAsync(
            source,
            cancellationToken);

    #endregion
}
