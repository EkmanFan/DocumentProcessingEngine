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

                var anchor =
                    block.Words[index - 1];

                if (anchor.MedianPointSize is null ||
                    !anchor.Text.Any(
                        char.IsLetterOrDigit))
                {
                    continue;
                }

                var pointSizeRatio =
                    marker.MedianPointSize.Value /
                    anchor.MedianPointSize.Value;

                if (pointSizeRatio <
                        MinimumPointSizeRatio ||
                    pointSizeRatio >
                        MaximumPointSizeRatio)
                {
                    continue;
                }

                var horizontalGap =
                    marker.Bounds.Left -
                    anchor.Bounds.Right;

                if (Math.Abs(
                        horizontalGap) >
                    MaximumHorizontalGap ||
                    CenterY(
                        marker) >=
                    CenterY(
                        anchor))
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
