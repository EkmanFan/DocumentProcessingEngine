using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Layout;

namespace DocumentProcessing.Engine.Reconciliation;

/// <summary>
/// Projects a native text block onto one OCR-authorized layout region.
///
/// The first and last native words with positive spatial intersection define
/// a contiguous source-block span. All words between those boundaries are
/// retained in source-block reading order so small geometry differences do not
/// silently reorder or punch holes in the comparable text extent.
/// </summary>
public static class NativeTextExtentProjector
{
    public static ComparableNativeTextExtent? Project(
        DocumentTextBlock sourceBlock,
        LayoutObservation sourceLayoutObservation)
    {
        ArgumentNullException.ThrowIfNull(sourceBlock);
        ArgumentNullException.ThrowIfNull(sourceLayoutObservation);

        if (!LayoutTextPolicy.IsTextRecognitionCandidate(
                sourceLayoutObservation.Kind))
        {
            throw new InvalidOperationException(
                $"Layout kind {sourceLayoutObservation.Kind} is not authorized " +
                "for text recognition/reconciliation.");
        }

        if (!HasPositiveIntersection(
                sourceBlock.Bounds,
                sourceLayoutObservation.Bounds))
        {
            return null;
        }

        var firstWordIndex =
            -1;

        var lastWordIndex =
            -1;

        var intersectingWordCount =
            0;

        for (var index = 0;
             index < sourceBlock.Words.Count;
             index++)
        {
            if (!HasPositiveIntersection(
                    sourceBlock.Words[index].Bounds,
                    sourceLayoutObservation.Bounds))
            {
                continue;
            }

            if (firstWordIndex < 0)
            {
                firstWordIndex =
                    index;
            }

            lastWordIndex =
                index;

            intersectingWordCount++;
        }

        if (firstWordIndex < 0)
        {
            return null;
        }

        var words =
            sourceBlock.Words
                .Skip(firstWordIndex)
                .Take(
                    lastWordIndex -
                    firstWordIndex +
                    1)
                .ToArray();

        return new ComparableNativeTextExtent(
            sourceBlock,
            sourceLayoutObservation,
            firstWordIndex,
            lastWordIndex,
            intersectingWordCount,
            words);
    }

    private static bool HasPositiveIntersection(
        NormalizedRectangle left,
        NormalizedRectangle right)
    {
        var intersectionLeft =
            Math.Max(
                left.Left,
                right.Left);

        var intersectionTop =
            Math.Max(
                left.Top,
                right.Top);

        var intersectionRight =
            Math.Min(
                left.Right,
                right.Right);

        var intersectionBottom =
            Math.Min(
                left.Bottom,
                right.Bottom);

        return intersectionRight >
                   intersectionLeft &&
               intersectionBottom >
                   intersectionTop;
    }
}
