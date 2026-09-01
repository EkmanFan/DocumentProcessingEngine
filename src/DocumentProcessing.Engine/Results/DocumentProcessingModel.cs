using DocumentProcessing.Core.DocumentModel;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Engine.Results;

/// <summary>
/// Engine-internal carrier that keeps the paged processing model and projected
/// semantic notes together until the portable-result boundary.
/// </summary>
internal sealed record DocumentProcessingModel
{
    #region Properties

    public PagedDocumentProcessingModel PagedModel { get; }

    public IReadOnlyList<DocumentNote> Notes { get; }

    #endregion

    #region ctor

    public DocumentProcessingModel(
        PagedDocumentProcessingModel pagedModel,
        IReadOnlyList<DocumentNote> notes)
    {
        PagedModel =
            pagedModel ??
            throw new ArgumentNullException(
                nameof(pagedModel));

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
