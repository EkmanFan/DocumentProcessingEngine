using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Engine.Footnotes;

/// <summary>
/// Engine-internal source-block identity used only while reconstructing
/// footnote topology from neutral native evidence.
/// </summary>
internal readonly record struct FootnoteSourceBlockKey(
    int PhysicalPageNumber,
    int SourceSequence);

/// <summary>
/// One raised inline numeric reference correlated to a footnote label.
/// </summary>
internal sealed record FootnoteReferenceTopology(
    string Label,
    int PhysicalPageNumber,
    int SourceBlockSequence,
    int WordSourceSequence,
    NormalizedRectangle Bounds);

/// <summary>
/// One visual payload line retained as source custody for a footnote entry.
/// </summary>
internal sealed record FootnotePayloadLineTopology(
    int PhysicalPageNumber,
    string Text,
    NormalizedRectangle Bounds,
    IReadOnlyList<int> SourceBlockSequences,
    IReadOnlyList<int> WordSourceSequences);

/// <summary>
/// One Engine-recognized numeric footnote entry.
///
/// This is deliberately an internal processing model. F1b.6B will decide how
/// much of this evidence becomes part of the portable result contract.
/// </summary>
internal sealed record FootnoteEntryTopology
{
    #region Properties

    public string Label { get; }

    public IReadOnlyList<FootnoteReferenceTopology> References { get; }

    public IReadOnlyList<FootnotePayloadLineTopology> PayloadLines { get; }

    public IReadOnlyList<FootnoteSourceBlockKey> SourceBlocks { get; }

    public string Text { get; }

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

    public FootnoteEntryTopology(
        string label,
        IReadOnlyList<FootnoteReferenceTopology> references,
        IReadOnlyList<FootnotePayloadLineTopology> payloadLines,
        IReadOnlyList<FootnoteSourceBlockKey> sourceBlocks)
    {
        if (string.IsNullOrWhiteSpace(
                label))
        {
            throw new ArgumentException(
                "Footnote label cannot be empty.",
                nameof(label));
        }

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

        Label =
            label.Trim();

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

/// <summary>
/// Engine-internal result of deterministic footnote-topology analysis.
/// </summary>
internal sealed class FootnoteTopologyAnalysis
{
    #region Properties

    public IReadOnlyList<FootnoteEntryTopology> Entries { get; }

    public IReadOnlySet<FootnoteSourceBlockKey> ExcludedSourceBlocks { get; }

    #endregion


    #region ctor

    public FootnoteTopologyAnalysis(
        IReadOnlyList<FootnoteEntryTopology> entries)
    {
        ArgumentNullException.ThrowIfNull(
            entries);

        Entries =
            entries.ToArray();

        ExcludedSourceBlocks =
            Entries
                .SelectMany(
                    entry =>
                        entry.SourceBlocks)
                .ToHashSet();
    }

    #endregion


    #region Methods

    public bool ContainsSourceBlock(
        int physicalPageNumber,
        int sourceSequence) =>
        ExcludedSourceBlocks.Contains(
            new FootnoteSourceBlockKey(
                physicalPageNumber,
                sourceSequence));

    #endregion
}

/// <summary>
/// Minimal neutral page evidence accepted by the pure topology classifier.
/// </summary>
internal sealed record FootnotePageEvidence(
    int PhysicalPageNumber,
    IReadOnlyList<DocumentTextBlock> Blocks);
