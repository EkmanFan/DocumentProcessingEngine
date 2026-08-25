using DocumentProcessing.Core.DocumentModel;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Engine.Results;

/// <summary>
/// Engine-internal carrier that keeps the legacy ingestion result and newly
/// projected semantic footnotes together until the portable-result boundary.
/// </summary>
internal sealed record DocumentProcessingModel
{
    #region Properties

    public DocumentIngestionResult IngestionResult { get; }

    public IReadOnlyList<DocumentFootnote> Footnotes { get; }

    #endregion

    #region ctor

    public DocumentProcessingModel(
        DocumentIngestionResult ingestionResult,
        IReadOnlyList<DocumentFootnote> footnotes)
    {
        IngestionResult =
            ingestionResult ??
            throw new ArgumentNullException(
                nameof(ingestionResult));

        ArgumentNullException.ThrowIfNull(
            footnotes);

        var copy =
            footnotes.ToArray();

        if (copy.Any(
                footnote =>
                    footnote is null))
        {
            throw new ArgumentException(
                "Engine footnote projection cannot contain null values.",
                nameof(footnotes));
        }

        Footnotes =
            Array.AsReadOnly(
                copy);
    }

    #endregion
}
