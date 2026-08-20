using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Core.Results;

/// <summary>
/// Portable, format-neutral representation of one processed document element.
/// </summary>
/// <remarks>
/// The element owns documentary content and source location, but no physical
/// page assumption. PDF can supply <see cref="PagedDocumentSourceLocation"/>;
/// EPUB and DOCX can later supply location types matching their native
/// structure.
///
/// Processing-specific custody evidence is intentionally not copied into this
/// first structural contract. It remains in the proven V1 provenance model
/// until the later evidence migration. C2.1 therefore introduces no data-loss
/// path and does not replace the existing PDF result.
/// </remarks>
public sealed record DocumentElement
{
    #region ctor

    /// <summary>
    /// Creates one portable document element.
    /// </summary>
    /// <param name="elementId">Stable element identifier within the result.</param>
    /// <param name="ordinal">Zero-based document-wide element order.</param>
    /// <param name="kind">Portable semantic element kind.</param>
    /// <param name="location">Format-appropriate source location.</param>
    /// <param name="segmentId">
    /// Optional structural segment containing this element.
    /// </param>
    /// <param name="text">
    /// Authoritative final text for textual element kinds; otherwise null.
    /// </param>
    /// <param name="textSha256">
    /// SHA-256 of the exact UTF-8 <paramref name="text"/>; otherwise null.
    /// </param>
    public DocumentElement(
        string elementId,
        int ordinal,
        DocumentElementKind kind,
        DocumentSourceLocation location,
        string? segmentId,
        string? text,
        string? textSha256)
    {
        if (string.IsNullOrWhiteSpace(
                elementId))
        {
            throw new ArgumentException(
                "Element ID cannot be empty.",
                nameof(elementId));
        }

        if (ordinal < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ordinal));
        }

        Location =
            location ??
            throw new ArgumentNullException(
                nameof(location));

        var requiresAuthoritativeText =
            kind is
                DocumentElementKind.Text or
                DocumentElementKind.Heading or
                DocumentElementKind.Caption;

        if (requiresAuthoritativeText)
        {
            if (string.IsNullOrWhiteSpace(
                    text))
            {
                throw new ArgumentException(
                    "Textual document elements must contain authoritative text.",
                    nameof(text));
            }

            if (string.IsNullOrWhiteSpace(
                    textSha256))
            {
                throw new ArgumentException(
                    "Textual document elements must contain an authoritative text SHA-256.",
                    nameof(textSha256));
            }
        }
        else if (text is not null ||
                 textSha256 is not null)
        {
            throw new ArgumentException(
                "Non-textual, unresolved, and deferred elements cannot contain authoritative text or its hash.");
        }

        if (text is not null &&
            !ProvenanceTextHashing.MatchesUtf8Sha256(
                text,
                textSha256!))
        {
            throw new ArgumentException(
                "Element text SHA-256 does not match the exact UTF-8 text.",
                nameof(textSha256));
        }

        ElementId =
            elementId.Trim();

        Ordinal =
            ordinal;

        Kind =
            kind;

        SegmentId =
            string.IsNullOrWhiteSpace(
                segmentId)
                ? null
                : segmentId.Trim();

        Text =
            text;

        TextSha256 =
            textSha256?.Trim()
                .ToLowerInvariant();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the stable element identifier within the result.
    /// </summary>
    public string ElementId { get; }

    /// <summary>
    /// Gets the zero-based document-wide element order.
    /// </summary>
    public int Ordinal { get; }

    /// <summary>
    /// Gets the portable semantic element kind.
    /// </summary>
    public DocumentElementKind Kind { get; }

    /// <summary>
    /// Gets the format-appropriate location in the source document.
    /// </summary>
    public DocumentSourceLocation Location { get; }

    /// <summary>
    /// Gets the optional structural segment containing this element.
    /// </summary>
    public string? SegmentId { get; }

    /// <summary>
    /// Gets the authoritative final text, when this is a textual element.
    /// </summary>
    public string? Text { get; }

    /// <summary>
    /// Gets the SHA-256 of the exact UTF-8 <see cref="Text"/>, when present.
    /// </summary>
    public string? TextSha256 { get; }

    #endregion
}
