using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Core.Processing;

/// <summary>
/// Defines one format-specific document-processing strategy.
/// </summary>
/// <remarks>
/// A processor owns both format recognition and processing for its declared
/// format. Generic routing asks only whether the processor can handle a source;
/// the validator used to answer that question remains encapsulated.
/// </remarks>
public interface IDocumentFormatProcessor
{
    #region Properties

    DocumentFormatId Format { get; }

    #endregion

    #region Methods Validation

    ValueTask<bool> ValidateAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default);

    #endregion

    #region Methods Processing

    Task<DocumentProcessingResult> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default);

    #endregion
}
