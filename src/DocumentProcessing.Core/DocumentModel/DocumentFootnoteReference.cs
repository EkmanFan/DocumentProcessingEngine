namespace DocumentProcessing.Core.DocumentModel;

/// <summary>
/// One inline reference to a semantic footnote.
/// </summary>
public sealed record DocumentFootnoteReference
{
    #region Properties

    /// <summary>
    /// Gets the stable element/source-location provenance of this marker.
    /// </summary>
    public DocumentFootnoteProvenance Provenance { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one footnote reference from autonomous marker provenance.
    /// </summary>
    public DocumentFootnoteReference(
        DocumentFootnoteProvenance provenance)
    {
        Provenance =
            provenance ??
            throw new ArgumentNullException(
                nameof(provenance));
    }

    #endregion
}
