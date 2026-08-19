using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Processing;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.Formats.Pdf;

/// <summary>
/// Transitional PDF implementation of the generic document-format strategy.
/// </summary>
/// <remarks>
/// This adapter lives in the top-level composition assembly only while the
/// existing authoritative PDF-shaped <see cref="DocumentProcessor"/> remains
/// in DocumentProcessing.Engine.
///
/// Keeping the bridge here preserves the repository dependency contract:
/// DocumentProcessing.Engine and DocumentProcessing.Pdf remain sibling modules
/// that do not reference each other. The composition assembly is allowed to
/// know the concrete pieces it wires together.
///
/// B2 will progressively move PDF orchestration behind the PDF module. Once
/// that ownership is correct, this strategy can move to DocumentProcessing.Pdf
/// and this transitional bridge disappears.
///
/// The current inner processor still performs its own format detection. That
/// duplicate detection is accepted temporarily to keep A1 behavior-preserving.
/// </remarks>
public sealed class PdfDocumentFormatProcessor
    : IDocumentFormatProcessor
{
    #region Variables and Constants

    private readonly DocumentProcessor _documentProcessor;
    private readonly PdfPreservedVisualDestinationFactory?
        _openPreservedVisualDestinationAsync;

    #endregion

    #region ctor

    /// <summary>
    /// Creates the temporary PDF strategy around the current authoritative
    /// processor.
    /// </summary>
    /// <param name="documentProcessor">
    /// Existing authoritative processor used until B2 relocates PDF
    /// orchestration.
    /// </param>
    /// <param name="openPreservedVisualDestinationAsync">
    /// Optional caller-owned destination factory for meaningful PDF visuals.
    /// </param>
    public PdfDocumentFormatProcessor(
        DocumentProcessor documentProcessor,
        PdfPreservedVisualDestinationFactory?
            openPreservedVisualDestinationAsync = null)
    {
        _documentProcessor =
            documentProcessor ??
            throw new ArgumentNullException(
                nameof(documentProcessor));

        _openPreservedVisualDestinationAsync =
            openPreservedVisualDestinationAsync;
    }

    #endregion

    #region Properties

    /// <inheritdoc />
    public DocumentFormatId Format =>
        DocumentFormatId.Pdf;

    #endregion

    #region Methods Processing

    /// <inheritdoc />
    public Task<DocumentIngestionResult> ProcessDocumentAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        return _openPreservedVisualDestinationAsync is null
            ? _documentProcessor.ProcessAsync(
                source,
                cancellationToken)
            : _documentProcessor.ProcessAsync(
                source,
                (visual, token) =>
                    _openPreservedVisualDestinationAsync(
                        source,
                        visual,
                        token),
                cancellationToken);
    }

    #endregion
}
