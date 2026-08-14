using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Core.Reconciliation;

/// <summary>
/// A contiguous page-local span of native words selected as the spatially
/// comparable native evidence for one layout/OCR region.
///
/// This is evidence projection, not reconciliation. No OCR text, fuzzy score,
/// dehyphenation, or authority decision is used to construct the extent.
/// </summary>
public sealed class ComparableNativeTextExtent
{
    public ComparableNativeTextExtent(
        DocumentTextBlock sourceBlock,
        LayoutObservation sourceLayoutObservation,
        int firstWordIndex,
        int lastWordIndex,
        int intersectingWordCount,
        IReadOnlyList<DocumentWord> words)
    {
        ArgumentNullException.ThrowIfNull(sourceBlock);
        ArgumentNullException.ThrowIfNull(sourceLayoutObservation);
        ArgumentNullException.ThrowIfNull(words);

        if (firstWordIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstWordIndex));
        }

        if (lastWordIndex < firstWordIndex)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastWordIndex));
        }

        if (lastWordIndex >= sourceBlock.Words.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastWordIndex));
        }

        var expectedWordCount =
            lastWordIndex -
            firstWordIndex +
            1;

        if (words.Count != expectedWordCount)
        {
            throw new ArgumentException(
                "Comparable extent words must exactly represent the contiguous " +
                "source-block word span.",
                nameof(words));
        }

        if (intersectingWordCount <= 0 ||
            intersectingWordCount > words.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intersectingWordCount));
        }

        for (var index = 0;
             index < words.Count;
             index++)
        {
            if (!ReferenceEquals(
                    words[index],
                    sourceBlock.Words[firstWordIndex + index]))
            {
                throw new ArgumentException(
                    "Comparable extent words must preserve source-block word order " +
                    "and references.",
                    nameof(words));
            }
        }

        SourceBlock =
            sourceBlock;

        SourceLayoutObservation =
            sourceLayoutObservation;

        FirstWordIndex =
            firstWordIndex;

        LastWordIndex =
            lastWordIndex;

        IntersectingWordCount =
            intersectingWordCount;

        Words =
            words.ToArray();

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

    public DocumentTextBlock SourceBlock { get; }

    public LayoutObservation SourceLayoutObservation { get; }

    public int FirstWordIndex { get; }

    public int LastWordIndex { get; }

    public int IntersectingWordCount { get; }

    public IReadOnlyList<DocumentWord> Words { get; }

    public int WordCount =>
        Words.Count;

    /// <summary>
    /// Raw projected text joined from native word evidence in block reading
    /// order. Phase 17C intentionally performs no dehyphenation.
    /// </summary>
    public string Text { get; }

    public NormalizedRectangle Bounds { get; }

    private static NormalizedRectangle Union(
        IReadOnlyList<DocumentWord> words)
    {
        if (words.Count == 0)
        {
            throw new ArgumentException(
                "Comparable extent must contain at least one word.",
                nameof(words));
        }

        var left =
            words.Min(
                word =>
                    word.Bounds.Left);

        var top =
            words.Min(
                word =>
                    word.Bounds.Top);

        var right =
            words.Max(
                word =>
                    word.Bounds.Right);

        var bottom =
            words.Max(
                word =>
                    word.Bounds.Bottom);

        return new NormalizedRectangle(
            left,
            top,
            right,
            bottom);
    }
}
