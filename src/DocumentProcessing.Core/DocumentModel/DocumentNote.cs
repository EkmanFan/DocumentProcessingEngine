using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Provenance;

namespace DocumentProcessing.Core.DocumentModel;

/// <summary>
/// One semantic note projected outside the primary document reading flow.
/// </summary>
public sealed record DocumentNote
{
    #region Properties

    /// <summary>
    /// Gets the stable note identifier within this processing result.
    /// </summary>
    public string NoteId { get; }

    /// <summary>
    /// Gets the zero-based document-wide note order.
    /// </summary>
    public int Ordinal { get; }

    /// <summary>
    /// Gets the source-visible note label.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the authoritative reconstructed note text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the SHA-256 of the exact UTF-8 <see cref="Text"/>.
    /// </summary>
    public string TextSha256 { get; }

    /// <summary>
    /// Gets source custody for the semantic note payload.
    /// </summary>
    /// <remarks>
    /// Multiple locations are intentional. One semantic note may cross a
    /// source boundary without widening the location contract of every document
    /// element.
    /// </remarks>
    public IReadOnlyList<DocumentSourceLocation> SourceLocations { get; }

    /// <summary>
    /// Gets inline markers that reference this note.
    /// </summary>
    public IReadOnlyList<DocumentNoteReference> References { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one portable semantic note.
    /// </summary>
    public DocumentNote(
        string noteId,
        int ordinal,
        string label,
        string text,
        string textSha256,
        IReadOnlyList<DocumentSourceLocation> sourceLocations,
        IReadOnlyList<DocumentNoteReference> references)
    {
        if (string.IsNullOrWhiteSpace(
                noteId))
        {
            throw new ArgumentException(
                "Note ID cannot be empty.",
                nameof(noteId));
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
                "Note label cannot be empty.",
                nameof(label));
        }

        if (string.IsNullOrWhiteSpace(
                text))
        {
            throw new ArgumentException(
                "Note text cannot be empty.",
                nameof(text));
        }

        if (string.IsNullOrWhiteSpace(
                textSha256))
        {
            throw new ArgumentException(
                "Note text SHA-256 cannot be empty.",
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
                "Note text SHA-256 must contain exactly 64 hexadecimal characters.",
                nameof(textSha256));
        }

        if (!ProvenanceTextHashing
                .MatchesUtf8Sha256(
                    text,
                    normalizedHash))
        {
            throw new ArgumentException(
                "Note text SHA-256 does not match the exact UTF-8 text.",
                nameof(textSha256));
        }

        var locations =
            sourceLocations.ToArray();

        if (locations.Length ==
            0)
        {
            throw new ArgumentException(
                "A semantic note requires source-location custody.",
                nameof(sourceLocations));
        }

        if (locations.Any(
                location =>
                    location is null))
        {
            throw new ArgumentException(
                "Note source locations cannot contain null values.",
                nameof(sourceLocations));
        }

        var referenceArray =
            references.ToArray();

        if (referenceArray.Length ==
            0)
        {
            throw new ArgumentException(
                "A semantic note requires at least one inline reference.",
                nameof(references));
        }

        if (referenceArray.Any(
                reference =>
                    reference is null))
        {
            throw new ArgumentException(
                "Note references cannot contain null values.",
                nameof(references));
        }

        NoteId =
            noteId.Trim();

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
