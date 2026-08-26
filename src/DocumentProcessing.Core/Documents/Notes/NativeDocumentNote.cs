using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Documents.Notes;

/// <summary>
/// Identifies one source text block contributing to a concluded paged note.
/// </summary>
public readonly record struct PagedNativeNoteSourceBlock
{
    #region Properties

    /// <summary>
    /// Gets the one-based physical page containing the source block.
    /// </summary>
    public int PhysicalPageNumber { get; }

    /// <summary>
    /// Gets the zero-based source sequence of the block on its page.
    /// </summary>
    public int SourceSequence { get; }

    #endregion

    #region ctor

    public PagedNativeNoteSourceBlock(
        int physicalPageNumber,
        int sourceSequence)
    {
        if (physicalPageNumber <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (sourceSequence <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceSequence));
        }

        PhysicalPageNumber =
            physicalPageNumber;

        SourceSequence =
            sourceSequence;
    }

    #endregion
}

/// <summary>
/// One raised inline numeric reference correlated to a footnote label.
/// </summary>
public sealed record PagedNativeNoteReference
{
    #region Properties

    /// <summary>
    /// Gets the source-visible note label.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the one-based physical page containing the reference.
    /// </summary>
    public int PhysicalPageNumber { get; }

    /// <summary>
    /// Gets the zero-based source sequence of the containing block.
    /// </summary>
    public int SourceBlockSequence { get; }

    /// <summary>
    /// Gets the zero-based source sequence of the reference word.
    /// </summary>
    public int WordSourceSequence { get; }

    /// <summary>
    /// Gets the normalized source bounds of the reference.
    /// </summary>
    public NormalizedRectangle Bounds { get; }

    #endregion

    #region ctor

    public PagedNativeNoteReference(
        string label,
        int physicalPageNumber,
        int sourceBlockSequence,
        int wordSourceSequence,
        NormalizedRectangle bounds)
    {
        if (string.IsNullOrWhiteSpace(
                label))
        {
            throw new ArgumentException(
                "Native note-reference label cannot be empty.",
                nameof(label));
        }

        if (physicalPageNumber <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (sourceBlockSequence <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceBlockSequence));
        }

        if (wordSourceSequence <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wordSourceSequence));
        }

        Label =
            label.Trim();

        PhysicalPageNumber =
            physicalPageNumber;

        SourceBlockSequence =
            sourceBlockSequence;

        WordSourceSequence =
            wordSourceSequence;

        Bounds =
            bounds;
    }

    #endregion
}

/// <summary>
/// One visual payload line retained as source custody for a footnote entry.
/// </summary>
public sealed record PagedNativeNotePayloadLine
{
    #region Properties

    /// <summary>
    /// Gets the one-based physical page containing the payload line.
    /// </summary>
    public int PhysicalPageNumber { get; }

    /// <summary>
    /// Gets the source-visible payload text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the normalized source bounds of the payload line.
    /// </summary>
    public NormalizedRectangle Bounds { get; }

    /// <summary>
    /// Gets the source blocks contributing words to the payload line.
    /// </summary>
    public IReadOnlyList<int> SourceBlockSequences { get; }

    /// <summary>
    /// Gets the source sequences of the words forming the payload line.
    /// </summary>
    public IReadOnlyList<int> WordSourceSequences { get; }

    #endregion

    #region ctor

    public PagedNativeNotePayloadLine(
        int physicalPageNumber,
        string text,
        NormalizedRectangle bounds,
        IReadOnlyList<int> sourceBlockSequences,
        IReadOnlyList<int> wordSourceSequences)
    {
        if (physicalPageNumber <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (string.IsNullOrWhiteSpace(
                text))
        {
            throw new ArgumentException(
                "Native note payload-line text cannot be empty.",
                nameof(text));
        }

        ArgumentNullException.ThrowIfNull(
            sourceBlockSequences);

        ArgumentNullException.ThrowIfNull(
            wordSourceSequences);

        if (sourceBlockSequences.Count ==
                0 ||
            sourceBlockSequences.Any(
                sequence =>
                    sequence <
                    0))
        {
            throw new ArgumentException(
                "Native note payload lines require non-negative source-block sequences.",
                nameof(sourceBlockSequences));
        }

        if (wordSourceSequences.Count ==
                0 ||
            wordSourceSequences.Any(
                sequence =>
                    sequence <
                    0))
        {
            throw new ArgumentException(
                "Native note payload lines require non-negative word sequences.",
                nameof(wordSourceSequences));
        }

        PhysicalPageNumber =
            physicalPageNumber;

        Text =
            text.Trim();

        Bounds =
            bounds;

        SourceBlockSequences =
            sourceBlockSequences
                .Distinct()
                .OrderBy(
                    sequence =>
                        sequence)
                .ToArray();

        WordSourceSequences =
            wordSourceSequences.ToArray();
    }

    #endregion
}

/// <summary>
/// Common contract for a note relation concluded by a format adapter from its
/// native representation.
/// </summary>
public abstract record NativeDocumentNote
{
    #region Properties

    /// <summary>
    /// Gets the source-visible note label.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the normalized textual payload of the note.
    /// </summary>
    public abstract string Text { get; }

    #endregion

    #region ctor

    private protected NativeDocumentNote(
        string label)
    {
        if (string.IsNullOrWhiteSpace(
                label))
        {
            throw new ArgumentException(
                "Native note label cannot be empty.",
                nameof(label));
        }

        Label =
            label.Trim();
    }

    #endregion
}

/// <summary>
/// A note relation concluded from a physically paged native representation.
/// </summary>
public sealed record PagedNativeDocumentNote
    : NativeDocumentNote
{
    #region Properties

    /// <summary>
    /// Gets the inline references correlated to the note payload.
    /// </summary>
    public IReadOnlyList<PagedNativeNoteReference> References { get; }

    /// <summary>
    /// Gets the visual payload lines retained with source custody.
    /// </summary>
    public IReadOnlyList<PagedNativeNotePayloadLine> PayloadLines { get; }

    /// <summary>
    /// Gets the source blocks contributing to the note payload.
    /// </summary>
    public IReadOnlyList<PagedNativeNoteSourceBlock> SourceBlocks { get; }

    /// <inheritdoc />
    public override string Text { get; }

    /// <summary>
    /// Gets whether the note payload spans more than one physical page.
    /// </summary>
    public bool SpansMultiplePages =>
        PayloadLines
            .Select(
                line =>
                    line.PhysicalPageNumber)
            .Distinct()
            .Skip(1)
            .Any();

    #endregion

    #region ctor

    public PagedNativeDocumentNote(
        string label,
        IReadOnlyList<PagedNativeNoteReference> references,
        IReadOnlyList<PagedNativeNotePayloadLine> payloadLines,
        IReadOnlyList<PagedNativeNoteSourceBlock> sourceBlocks)
        : base(
            label)
    {
        ArgumentNullException.ThrowIfNull(
            references);

        ArgumentNullException.ThrowIfNull(
            payloadLines);

        ArgumentNullException.ThrowIfNull(
            sourceBlocks);

        if (references.Count == 0)
        {
            throw new ArgumentException(
                "A recognized footnote requires at least one correlated inline reference.",
                nameof(references));
        }

        if (payloadLines.Count == 0)
        {
            throw new ArgumentException(
                "A recognized footnote requires retained payload evidence.",
                nameof(payloadLines));
        }

        if (sourceBlocks.Count == 0)
        {
            throw new ArgumentException(
                "A recognized footnote requires source-block custody.",
                nameof(sourceBlocks));
        }

        if (references.Any(
                reference =>
                    reference is null) ||
            payloadLines.Any(
                line =>
                    line is null))
        {
            throw new ArgumentException(
                "A native note cannot contain null reference or payload evidence.");
        }

        if (references.Any(
                reference =>
                    !string.Equals(
                        reference.Label,
                        Label,
                        StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Native note-reference labels must match their owning note label.",
                nameof(references));
        }

        References =
            references.ToArray();

        PayloadLines =
            payloadLines.ToArray();

        SourceBlocks =
            sourceBlocks
                .Distinct()
                .OrderBy(
                    block =>
                        block.PhysicalPageNumber)
                .ThenBy(
                    block =>
                        block.SourceSequence)
                .ToArray();

        Text =
            string.Join(
                " ",
                PayloadLines
                    .Select(
                        line =>
                            line.Text)
                    .Where(
                        text =>
                            !string.IsNullOrWhiteSpace(
                                text)))
                .Trim();
    }

    #endregion
}
