using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Core.Reconciliation;

/// <summary>
/// Target-centric native text evidence for one OCR-authorized layout region.
///
/// One layout target may receive native evidence from one or more source
/// blocks. The component ComparableNativeTextExtent values preserve the
/// original source-block provenance while this object exposes one deterministic
/// aggregate for comparison against the target OCR region.
///
/// This remains evidence projection only. It performs no OCR comparison,
/// similarity scoring, authority selection, or text-quality decision.
/// </summary>
public sealed class ComparableNativeTextEvidence
{
    public ComparableNativeTextEvidence(
        LayoutObservation sourceLayoutObservation,
        IReadOnlyList<ComparableNativeTextExtent> extents)
    {
        ArgumentNullException.ThrowIfNull(sourceLayoutObservation);
        ArgumentNullException.ThrowIfNull(extents);

        if (extents.Count == 0)
        {
            throw new ArgumentException(
                "Comparable native text evidence requires at least one extent.",
                nameof(extents));
        }

        if (extents.Any(
                extent =>
                    extent is null))
        {
            throw new ArgumentException(
                "Comparable native text evidence cannot contain null extents.",
                nameof(extents));
        }

        if (extents.Any(
                extent =>
                    !ReferenceEquals(
                        extent.SourceLayoutObservation,
                        sourceLayoutObservation)))
        {
            throw new ArgumentException(
                "All comparable native extents must originate from the same layout observation.",
                nameof(extents));
        }

        var duplicateSourceBlockSequence =
            extents
                .GroupBy(
                    extent =>
                        extent.SourceBlock.SourceSequence)
                .FirstOrDefault(
                    group =>
                        group.Count() > 1);

        if (duplicateSourceBlockSequence is not null)
        {
            throw new ArgumentException(
                "Comparable native text evidence cannot contain more than one extent " +
                "from the same source block.",
                nameof(extents));
        }

        var orderedExtents =
            extents
                .OrderBy(
                    extent =>
                        extent.SourceBlock.ReadingOrder is null)
                .ThenBy(
                    extent =>
                        extent.SourceBlock.ReadingOrder ??
                        int.MaxValue)
                .ThenBy(
                    extent =>
                        extent.SourceBlock.SourceSequence)
                .ToArray();

        var words =
            orderedExtents
                .SelectMany(
                    extent =>
                        extent.Words)
                .ToArray();

        var duplicateWordReference =
            words
                .GroupBy(
                    word =>
                        word,
                    ReferenceEqualityComparer.Instance)
                .Any(
                    group =>
                        group.Count() > 1);

        if (duplicateWordReference)
        {
            throw new ArgumentException(
                "Comparable native text evidence cannot contain the same native word " +
                "reference more than once.",
                nameof(extents));
        }

        SourceLayoutObservation =
            sourceLayoutObservation;

        Extents =
            orderedExtents;

        SourceBlocks =
            orderedExtents
                .Select(
                    extent =>
                        extent.SourceBlock)
                .ToArray();

        Words =
            words;

        IntersectingWordCount =
            orderedExtents.Sum(
                extent =>
                    extent.IntersectingWordCount);

        Text =
            string.Join(
                " ",
                Words.Select(
                    word =>
                        word.Text));

        Bounds =
            Union(
                Words);
    }

    public LayoutObservation SourceLayoutObservation { get; }

    /// <summary>
    /// Per-source-block comparable extents in deterministic native reading
    /// order. These retain source-block provenance and are not competing
    /// candidates.
    /// </summary>
    public IReadOnlyList<ComparableNativeTextExtent> Extents { get; }

    public IReadOnlyList<DocumentTextBlock> SourceBlocks { get; }

    public IReadOnlyList<DocumentWord> Words { get; }

    public int ExtentCount =>
        Extents.Count;

    public int WordCount =>
        Words.Count;

    public int IntersectingWordCount { get; }

    /// <summary>
    /// Raw aggregate native text assembled from projected word evidence in
    /// native block reading order. No dehyphenation is performed here.
    /// </summary>
    public string Text { get; }

    public NormalizedRectangle Bounds { get; }

    private static NormalizedRectangle Union(
        IReadOnlyList<DocumentWord> words)
    {
        if (words.Count == 0)
        {
            throw new ArgumentException(
                "Comparable native text evidence must contain at least one word.",
                nameof(words));
        }

        return new NormalizedRectangle(
            words.Min(
                word =>
                    word.Bounds.Left),
            words.Min(
                word =>
                    word.Bounds.Top),
            words.Max(
                word =>
                    word.Bounds.Right),
            words.Max(
                word =>
                    word.Bounds.Bottom));
    }
}
