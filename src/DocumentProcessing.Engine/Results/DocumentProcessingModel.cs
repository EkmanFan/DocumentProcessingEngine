using DocumentProcessing.Core.DocumentModel;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Engine.Results;

/// <summary>
/// Engine-internal carrier that keeps the legacy ingestion result and newly
/// projected semantic notes together until the portable-result boundary.
/// </summary>
internal sealed record DocumentProcessingModel
{
    #region Properties

    public DocumentIngestionResult IngestionResult { get; }

    public IReadOnlyList<DocumentNote> Notes { get; }

    #endregion

    #region ctor

    public DocumentProcessingModel(
        DocumentIngestionResult ingestionResult,
        IReadOnlyList<DocumentNote> notes)
    {
        IngestionResult =
            ingestionResult ??
            throw new ArgumentNullException(
                nameof(ingestionResult));

        ArgumentNullException.ThrowIfNull(
            notes);

        var copy =
            notes.ToArray();

        if (copy.Any(
                note =>
                    note is null))
        {
            throw new ArgumentException(
                "Engine note projection cannot contain null values.",
                nameof(notes));
        }

        Notes =
            Array.AsReadOnly(
                copy);
    }

    #endregion
}
