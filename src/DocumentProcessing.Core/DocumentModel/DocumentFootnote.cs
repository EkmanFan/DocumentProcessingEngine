using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Core.DocumentModel;

/// <summary>
/// One semantic footnote projected outside the primary document reading flow.
/// </summary>
public sealed record DocumentFootnote
{
    #region Properties

    /// <summary>
    /// Gets the stable footnote identifier within this processing result.
    /// </summary>
    public string FootnoteId { get; }

    /// <summary>
    /// Gets the zero-based document-wide footnote order.
    /// </summary>
    public int Ordinal { get; }

    /// <summary>
    /// Gets the source-visible footnote label.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the authoritative reconstructed footnote text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the SHA-256 of the exact UTF-8 <see cref="Text"/>.
    /// </summary>
    public string TextSha256 { get; }

    /// <summary>
    /// Gets source custody for the semantic footnote payload.
    /// </summary>
    /// <remarks>
    /// Multiple locations are intentional. One semantic footnote may cross a
    /// source boundary without widening the location contract of every document
    /// element.
    /// </remarks>
    public IReadOnlyList<DocumentSourceLocation> SourceLocations { get; }

    /// <summary>
    /// Gets inline markers that reference this footnote.
    /// </summary>
    public IReadOnlyList<DocumentFootnoteReference> References { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one portable semantic footnote.
    /// </summary>
    public DocumentFootnote(
        string footnoteId,
        int ordinal,
        string label,
        string text,
        string textSha256,
        IReadOnlyList<DocumentSourceLocation> sourceLocations,
        IReadOnlyList<DocumentFootnoteReference> references)
    {
        if (string.IsNullOrWhiteSpace(
                footnoteId))
        {
            throw new ArgumentException(
                "Footnote ID cannot be empty.",
                nameof(footnoteId));
        }

        if (ordinal <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ordinal));
        }

        if (string.IsNullOrWhiteSpace(
                label))
        {
            throw new ArgumentException(
                "Footnote label cannot be empty.",
                nameof(label));
        }

        if (string.IsNullOrWhiteSpace(
                text))
        {
            throw new ArgumentException(
                "Footnote text cannot be empty.",
                nameof(text));
        }

        if (string.IsNullOrWhiteSpace(
                textSha256))
        {
            throw new ArgumentException(
                "Footnote text SHA-256 cannot be empty.",
                nameof(textSha256));
        }

        ArgumentNullException.ThrowIfNull(
            sourceLocations);

        ArgumentNullException.ThrowIfNull(
            references);

        var normalizedHash =
            textSha256.Trim()
                .ToLowerInvariant();

        if (normalizedHash.Length !=
                64 ||
            normalizedHash.Any(
                character =>
                    !Uri.IsHexDigit(
                        character)))
        {
            throw new ArgumentException(
                "Footnote text SHA-256 must contain exactly 64 hexadecimal characters.",
                nameof(textSha256));
        }

        if (!ProvenanceTextHashing
                .MatchesUtf8Sha256(
                    text,
                    normalizedHash))
        {
            throw new ArgumentException(
                "Footnote text SHA-256 does not match the exact UTF-8 text.",
                nameof(textSha256));
        }

        var locations =
            sourceLocations.ToArray();

        if (locations.Length ==
            0)
        {
            throw new ArgumentException(
                "A semantic footnote requires source-location custody.",
                nameof(sourceLocations));
        }

        if (locations.Any(
                location =>
                    location is null))
        {
            throw new ArgumentException(
                "Footnote source locations cannot contain null values.",
                nameof(sourceLocations));
        }

        var referenceArray =
            references.ToArray();

        if (referenceArray.Length ==
            0)
        {
            throw new ArgumentException(
                "A semantic footnote requires at least one inline reference.",
                nameof(references));
        }

        if (referenceArray.Any(
                reference =>
                    reference is null))
        {
            throw new ArgumentException(
                "Footnote references cannot contain null values.",
                nameof(references));
        }

        FootnoteId =
            footnoteId.Trim();

        Ordinal =
            ordinal;

        Label =
            label.Trim();

        Text =
            text;

        TextSha256 =
            normalizedHash;

        SourceLocations =
            Array.AsReadOnly(
                locations);

        References =
            Array.AsReadOnly(
                referenceArray);
    }

    #endregion
}
