using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Core.Processing;

/// <summary>
/// Defines one format-specific document-processing strategy.
/// </summary>
/// <remarks>
/// The generic engine depends on this contract instead of depending on PDF,
/// EPUB, DOCX, or any other concrete document-format implementation.
///
/// A strategy owns the processing behavior that is specific to its declared
/// <see cref="Format"/> and returns the canonical format-neutral
/// <see cref="DocumentProcessingResult"/> contract.
/// </remarks>
public interface IDocumentFormatProcessor
{
    #region Properties

    /// <summary>
    /// Gets the canonical document format handled by this strategy.
    /// </summary>
    DocumentFormatId Format { get; }

    #endregion

    #region Methods Processing

    /// <summary>
    /// Processes one document source using the format-specific strategy.
    /// </summary>
    /// <param name="source">Source document to process.</param>
    /// <param name="cancellationToken">
    /// Token used to cancel the processing operation.
    /// </param>
    /// <returns>
    /// The current portable document-processing result.
    /// </returns>
    Task<DocumentProcessingResult> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default);

    #endregion
}
