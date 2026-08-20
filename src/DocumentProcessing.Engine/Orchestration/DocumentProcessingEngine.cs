using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Format-neutral document-processing orchestrator.
/// </summary>
/// <remarks>
/// The Engine owns format selection and neutral execution when configured with
/// explicit format-processing bindings. The existing selected-strategy overload
/// remains available during controlled Host migration.
///
/// B2.3A deliberately establishes that responsibility boundary before the
/// existing PDF-shaped <c>DocumentProcessor</c> is split. At this checkpoint
/// the selected strategy still executes the current authoritative PDF
/// orchestration internally. Later B2 work can move genuinely format-neutral
/// stages into this Engine without changing Host routing.
/// </remarks>
public sealed class DocumentProcessingEngine
{
    #region Variables and Constants

    private readonly IReadOnlyList<DocumentFormatProcessingBinding>?
        _bindings;

    private readonly DocumentFormatSelector?
        _formatSelector;

    #endregion

    #region ctor

    public DocumentProcessingEngine()
    {
    }

    public DocumentProcessingEngine(
        IEnumerable<DocumentFormatProcessingBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(
            bindings);

        var materialized =
            bindings
                .ToArray();

        if (materialized.Any(
                binding =>
                    binding is null))
        {
            throw new ArgumentException(
                "Document format processing bindings cannot contain null entries.",
                nameof(bindings));
        }

        _formatSelector =
            new DocumentFormatSelector(
                materialized
                    .Select(
                        binding =>
                            binding.DocumentFormat));

        _bindings =
            materialized;
    }

    #endregion

    #region Methods Configured Processing

    internal async Task<DocumentProcessingAttemptResult>
        ProcessConfiguredDocumentAsync(
            DocumentSource source,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        cancellationToken.ThrowIfCancellationRequested();

        var bindings =
            _bindings ??
            throw new InvalidOperationException(
                "DocumentProcessingEngine was not configured with document format processing bindings.");

        var formatSelector =
            _formatSelector ??
            throw new InvalidOperationException(
                "DocumentProcessingEngine format selection is unavailable because no bindings were configured.");

        await using var prepared =
            await PreparedDocumentSource
                .CreateAsync(
                    source,
                    cancellationToken)
                .ConfigureAwait(false);

        var selection =
            await formatSelector
                .SelectAsync(
                    prepared,
                    cancellationToken)
                .ConfigureAwait(false);

        switch (selection)
        {
            case DocumentFormatSelectionResult.NotRecognized:
                return new DocumentProcessingAttemptResult
                    .NotRecognized();

            case DocumentFormatSelectionResult.Invalid invalid:
                return new DocumentProcessingAttemptResult
                    .Invalid(
                        invalid.DocumentFormat.Format,
                        invalid.Reason);

            case DocumentFormatSelectionResult.Ambiguous ambiguous:
                return new DocumentProcessingAttemptResult
                    .Ambiguous(
                        ambiguous.Formats);

            case DocumentFormatSelectionResult.Success success:
            {
                var binding =
                    bindings.SingleOrDefault(
                        candidate =>
                            ReferenceEquals(
                                candidate.DocumentFormat,
                                success.DocumentFormat));

                if (binding is null)
                {
                    throw new InvalidOperationException(
                        $"Selected document format '{success.DocumentFormat.Format}' has no exact configured processing binding.");
                }

                var result =
                    await binding
                        .Processor
                        .ProcessPreparedEvidencePortableAsync(
                            prepared,
                            binding.Format,
                            success.Evidence,
                            openVisualDestinationAsync:
                                null,
                            cancellationToken)
                        .ConfigureAwait(false);

                return new DocumentProcessingAttemptResult
                    .Success(
                        binding.Format,
                        result);
            }

            default:
                throw new InvalidOperationException(
                    $"Unsupported document format selection result '{selection.GetType().FullName}'.");
        }
    }

    #endregion

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
