using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Format-neutral document-processing orchestrator.
/// </summary>
/// <remarks>
/// The Host owns format detection and strategy selection. The Engine receives
/// the already-selected strategy for the current document and owns the neutral
/// execution boundary.
///
/// B2.3A deliberately establishes that responsibility boundary before the
/// existing PDF-shaped <c>DocumentProcessor</c> is split. At this checkpoint
/// the selected strategy still executes the current authoritative PDF
/// orchestration internally. Later B2 work can move genuinely format-neutral
/// stages into this Engine without changing Host routing.
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
