using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Pdf.Notes;

/// <summary>
/// Finds raised inline numeric observations shared by PDF note strategies.
/// It does not conclude that an observation is a note reference.
/// </summary>
internal static class PdfRaisedNumericReferenceFinder
{
    #region Variables and Constants

    private const double MinimumPointSizeRatio =
        0.65;

    private const double MaximumPointSizeRatio =
        0.86;

    private const double MaximumHorizontalGap =
        0.01;

    private const double MinimumFallbackVerticalRiseRatio =
        0.10;

    private const double MaximumFallbackVerticalRiseRatio =
        1.00;

    #endregion

    #region Methods

    public static IReadOnlyList<PdfRaisedNumericReferenceCandidate> Find(
        int physicalPageNumber,
        IReadOnlyList<DocumentTextBlock> blocks)
    {
        if (physicalPageNumber <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        ArgumentNullException.ThrowIfNull(
            blocks);

        var references =
            new List<PdfRaisedNumericReferenceCandidate>();

        var pageWords =
            blocks
                .SelectMany(
                    block =>
                        block.Words)
                .GroupBy(
                    word =>
                        word.SourceSequence)
                .Select(
                    group =>
                        group.First())
                .ToArray();

        foreach (var block in
                 blocks)
        {
            for (var index = 1;
                 index <
                 block.Words.Count;
                 index++)
            {
                var marker =
                    block.Words[index];

                if (!IsNumericMarker(
                        marker.Text) ||
                    marker.MedianPointSize is null)
                {
                    continue;
                }

                var sourceAnchor =
                    block.Words[index - 1];

                var anchor =
                    IsStrictAnchor(
                        marker,
                        sourceAnchor)
                        ? sourceAnchor
                        : FindFallbackAnchor(
                            marker,
                            block.MedianPointSize,
                            pageWords);

                if (anchor is null)
                {
                    continue;
                }

                references.Add(
                    new PdfRaisedNumericReferenceCandidate(
                        marker.Text,
                        physicalPageNumber,
                        block.SourceSequence,
                        marker));
            }
        }

        return references;
    }

    private static DocumentWord? FindFallbackAnchor(
        DocumentWord marker,
        double? blockMedianPointSize,
        IReadOnlyList<DocumentWord> pageWords)
    {
        if (marker.MedianPointSize is null ||
            blockMedianPointSize is null)
        {
            return null;
        }

        var blockPointSizeRatio =
            marker.MedianPointSize.Value /
            blockMedianPointSize.Value;

        if (!IsCompatiblePointSizeRatio(
                blockPointSizeRatio))
        {
            return null;
        }

        var matches =
            pageWords
                .Where(
                    candidate =>
                        candidate.SourceSequence !=
                            marker.SourceSequence &&
                        IsFallbackAnchor(
                            marker,
                            candidate))
                .ToArray();

        return matches.Length ==
               1
            ? matches[0]
            : null;
    }

    private static bool IsStrictAnchor(
        DocumentWord marker,
        DocumentWord anchor)
    {
        if (marker.MedianPointSize is null ||
            anchor.MedianPointSize is null ||
            !anchor.Text.Any(
                char.IsLetterOrDigit))
        {
            return false;
        }

        var pointSizeRatio =
            marker.MedianPointSize.Value /
            anchor.MedianPointSize.Value;

        return IsCompatiblePointSizeRatio(
                   pointSizeRatio) &&
               IsHorizontallyAdjacent(
                   marker,
                   anchor) &&
               CenterY(
                   marker) <
               CenterY(
                   anchor);
    }

    private static bool IsFallbackAnchor(
        DocumentWord marker,
        DocumentWord anchor)
    {
        if (anchor.MedianPointSize is null ||
            !anchor.Text.Any(
                char.IsLetterOrDigit) ||
            !IsHorizontallyAdjacent(
                marker,
                anchor))
        {
            return false;
        }

        var anchorHeight =
            anchor.Bounds.Bottom -
            anchor.Bounds.Top;

        if (anchorHeight <=
                0 ||
            !double.IsFinite(
                anchorHeight))
        {
            return false;
        }

        var verticalRise =
            CenterY(
                anchor) -
            CenterY(
                marker);

        var verticalRiseRatio =
            verticalRise /
            anchorHeight;

        return verticalRiseRatio >=
                   MinimumFallbackVerticalRiseRatio &&
               verticalRiseRatio <=
                   MaximumFallbackVerticalRiseRatio;
    }

    private static bool IsCompatiblePointSizeRatio(
        double pointSizeRatio) =>
        pointSizeRatio >=
            MinimumPointSizeRatio &&
        pointSizeRatio <=
            MaximumPointSizeRatio;

    private static bool IsHorizontallyAdjacent(
        DocumentWord marker,
        DocumentWord anchor) =>
        Math.Abs(
            marker.Bounds.Left -
            anchor.Bounds.Right) <=
        MaximumHorizontalGap;

    private static bool IsNumericMarker(
        string value) =>
        value.Length is
                >= 1 and
                <= 4 &&
        value.All(
            char.IsAsciiDigit);

    private static double CenterY(
        DocumentWord word) =>
        (
            word.Bounds.Top +
            word.Bounds.Bottom
        ) /
        2.0;

    #endregion
}

/// <summary>
/// Raised inline numeric observation that still requires payload correlation
/// before it can become a concluded note reference.
/// </summary>
internal sealed record PdfRaisedNumericReferenceCandidate(
    string Value,
    int PhysicalPageNumber,
    int SourceBlockSequence,
    DocumentWord Word)
{
    #region Properties

    public PdfNativeNoteReferenceKey Key =>
        new(
            PhysicalPageNumber,
            SourceBlockSequence,
            Word.SourceSequence);

    #endregion
}
