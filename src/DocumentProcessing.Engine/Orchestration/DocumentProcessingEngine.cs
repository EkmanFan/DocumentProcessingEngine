using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Current migration orchestration surface.
/// </summary>
/// <remarks>
/// The selected-format processor overload remains the authoritative compatibility
/// path while the universal Engine processing cycle is introduced incrementally.
/// This type does not bind document formats to complete processors.
/// </remarks>
public sealed class DocumentProcessingEngine
{
    #region Methods Public Processing

    public async Task<DocumentProcessingResult> ProcessDocumentAsync(
        DocumentSource source,
        IDocumentFormatProcessor formatProcessor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            formatProcessor);

        cancellationToken.ThrowIfCancellationRequested();

        var result =
            await formatProcessor
                .ProcessDocumentAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        return result ??
               throw new InvalidDataException(
                   $"The selected document format processor for '{formatProcessor.Format}' returned no result.");
    }

    #endregion
}
