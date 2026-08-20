using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Core.Visual;

namespace DocumentProcessing.Pdf;

/// <summary>
/// PDF implementation of the generic document-format strategy.
/// </summary>
/// <remarks>
/// The processor owns PDF validation, PDF visual-destination semantics and
/// adaptation of the current authoritative PDF result into the portable
/// <see cref="DocumentProcessingResult"/> contract.
///
/// Concrete Engine construction is supplied through
/// <see cref="PdfDocumentExecution"/> and is deliberately outside this module.
/// </remarks>
public sealed class PdfDocumentFormatProcessor
    : IDocumentFormatProcessor
{
    #region Variables and Constants

    private readonly IFormatValidator _validator;
    private readonly PdfDocumentExecution _executeAsync;
    private readonly PreservedLayoutVisualDestinationFactory?
        _openPreservedVisualDestinationAsync;

    #endregion

    #region ctor

    public PdfDocumentFormatProcessor(
        PdfDocumentExecution executeAsync,
        PreservedLayoutVisualDestinationFactory?
            openPreservedVisualDestinationAsync = null)
    {
        _validator =
            new PdfFormatValidator();

        _executeAsync =
            executeAsync ??
            throw new ArgumentNullException(
                nameof(executeAsync));

        _openPreservedVisualDestinationAsync =
            openPreservedVisualDestinationAsync;
    }

    #endregion

    #region Properties

    public DocumentFormatId Format =>
        DocumentFormatId.Pdf;

    #endregion

    #region Methods Validation

    public ValueTask<bool> ValidateAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        return _validator.ValidateAsync(
            source,
            cancellationToken);
    }

    #endregion

    #region Methods Processing

    public async Task<DocumentProcessingResult> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        var authoritativeResult =
            await _executeAsync(
                    source,
                    _openPreservedVisualDestinationAsync,
                    cancellationToken)
                .ConfigureAwait(false);

        return PdfDocumentProcessingResultAdapter.Adapt(
            authoritativeResult);
    }

    #endregion
}
