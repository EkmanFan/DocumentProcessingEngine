using DocumentProcessing.Core.Locations;

namespace DocumentProcessing.Core.DocumentModel;

/// <summary>
/// Format-neutral provenance for one inline reference to a semantic footnote.
/// </summary>
/// <remarks>
/// This record is deliberately autonomous: it depends only on stable document
/// element identity plus a source location. It does not depend on an ingestion
/// result, a PDF implementation type, or an Engine-internal topology type.
/// </remarks>
public sealed record DocumentFootnoteProvenance
{
    #region Properties

    /// <summary>
    /// Gets the stable document-element identifier containing the inline marker.
    /// </summary>
    public string ElementId { get; }

    /// <summary>
    /// Gets the exact format-appropriate source location of the inline marker.
    /// </summary>
    public DocumentSourceLocation Location { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates reference provenance anchored to a stable document element.
    /// </summary>
    public DocumentFootnoteProvenance(
        string elementId,
        DocumentSourceLocation location)
    {
        if (string.IsNullOrWhiteSpace(
                elementId))
        {
            throw new ArgumentException(
                "Footnote reference element ID cannot be empty.",
                nameof(elementId));
        }

        ElementId =
            elementId.Trim();

        Location =
            location ??
            throw new ArgumentNullException(
                nameof(location));
    }

    #endregion
}
