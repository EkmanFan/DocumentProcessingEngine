namespace DocumentProcessing.Core.DocumentModel;

/// <summary>
/// One inline reference to a semantic note.
/// </summary>
public sealed record DocumentNoteReference
{
    #region Properties

    /// <summary>
    /// Gets the stable element/source-location provenance of this marker.
    /// </summary>
    public DocumentNoteProvenance Provenance { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one note reference from autonomous marker provenance.
    /// </summary>
    public DocumentNoteReference(
        DocumentNoteProvenance provenance)
    {
        Provenance =
            provenance ??
            throw new ArgumentNullException(
                nameof(provenance));
    }

    #endregion
}
