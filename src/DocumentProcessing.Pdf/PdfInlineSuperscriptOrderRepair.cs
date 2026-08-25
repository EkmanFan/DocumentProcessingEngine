using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace DocumentProcessing.Pdf;

/// <summary>
/// Repairs a narrow PdfPig layout artifact where a raised numeric marker is
/// emitted as its own layout line immediately before the body line containing
/// the word to which the marker is spatially attached.
///
/// This is PDF-native reconstruction only. It does not classify the marker as
/// a footnote reference and it does not change source provenance.
/// </summary>
internal static class PdfInlineSuperscriptOrderRepair
{
    #region Variables and Constants

    private const double MinimumMarkerToAnchorPointSizeRatio =
        0.65;

    private const double MaximumMarkerToAnchorPointSizeRatio =
        0.86;

    private const double MaximumHorizontalGapToAnchorPointSizeRatio =
        0.10;

    private const double MinimumVerticalRiseToAnchorPointSizeRatio =
        0.15;

    private const double MaximumVerticalRiseToAnchorPointSizeRatio =
        0.55;

    #endregion


    #region Methods

    public static PdfInlineTextBlockReconstruction Reconstruct(
        TextBlock block)
    {
        ArgumentNullException.ThrowIfNull(
            block);

        var sourceLines =
            block.TextLines
                .Select(line =>
                    line.Words.ToArray())
                .ToArray();

        var originalWords =
            sourceLines
                .SelectMany(words =>
                    words)
                .ToArray();

        if (sourceLines.Length < 2)
        {
            return new PdfInlineTextBlockReconstruction(
                block.Text,
                originalWords);
        }

        var sourceTextLines =
            SplitSourceTextLines(
                block.Text);

        if (sourceTextLines.Length !=
            sourceLines.Length)
        {
            return new PdfInlineTextBlockReconstruction(
                block.Text,
                originalWords);
        }

        var repairedLines =
            sourceLines
                .Select(words =>
                    words.ToList())
                .ToArray();

        var removedLines =
            new bool[
                sourceLines.Length];

        var modifiedLines =
            new bool[
                sourceLines.Length];

        for (var lineIndex = 0;
             lineIndex <
             sourceLines.Length - 1;
             lineIndex++)
        {
            var markerLine =
                sourceLines[
                    lineIndex];

            if (markerLine.Length != 1)
            {
                continue;
            }

            var marker =
                markerLine[0];

            if (!IsRaisedNumericMarkerText(
                    marker.Text))
            {
                continue;
            }

            var targetLineIndex =
                lineIndex + 1;

            var targetWords =
                sourceLines[
                    targetLineIndex];

            if (targetWords.Length == 0)
            {
                continue;
            }

            var markerGeometry =
                ToGeometry(
                    marker);

            if (markerGeometry is null)
            {
                continue;
            }

            var targetGeometries =
                targetWords
                    .Select(
                        ToGeometry)
                    .ToArray();

            if (targetGeometries.Any(
                    geometry =>
                        geometry is null))
            {
                continue;
            }

            var anchorIndex =
                FindAnchorIndex(
                    markerGeometry.Value,
                    targetGeometries
                        .Select(geometry =>
                            geometry!.Value)
                        .ToArray());

            if (anchorIndex is null)
            {
                continue;
            }

            repairedLines[
                    targetLineIndex]
                .Insert(
                    anchorIndex.Value + 1,
                    marker);

            removedLines[
                lineIndex] =
                true;

            modifiedLines[
                targetLineIndex] =
                true;
        }

        if (!removedLines.Any(
                removed =>
                    removed))
        {
            return new PdfInlineTextBlockReconstruction(
                block.Text,
                originalWords);
        }

        var repairedWords =
            new List<Word>(
                originalWords.Length);

        var repairedTextLines =
            new List<string>(
                sourceLines.Length);

        for (var lineIndex = 0;
             lineIndex < sourceLines.Length;
             lineIndex++)
        {
            if (removedLines[
                    lineIndex])
            {
                continue;
            }

            var lineWords =
                repairedLines[
                    lineIndex];

            repairedWords.AddRange(
                lineWords);

            repairedTextLines.Add(
                modifiedLines[
                    lineIndex]
                    ? string.Join(
                        " ",
                        lineWords.Select(word =>
                            word.Text))
                    : sourceTextLines[
                        lineIndex]);
        }

        return new PdfInlineTextBlockReconstruction(
            string.Join(
                "\n",
                repairedTextLines),
            repairedWords);
    }

    internal static int? FindAnchorIndex(
        PdfInlineWordGeometry marker,
        IReadOnlyList<PdfInlineWordGeometry> anchors)
    {
        ArgumentNullException.ThrowIfNull(
            anchors);

        var matches =
            anchors
                .Select(
                    (anchor, index) =>
                        new
                        {
                            Index =
                                index,
                            IsCompatible =
                                IsCompatibleAnchor(
                                    marker,
                                    anchor)
                        })
                .Where(candidate =>
                    candidate.IsCompatible)
                .Select(candidate =>
                    candidate.Index)
                .ToArray();

        return matches.Length == 1
            ? matches[0]
            : null;
    }

    private static bool IsCompatibleAnchor(
        PdfInlineWordGeometry marker,
        PdfInlineWordGeometry anchor)
    {
        if (marker.MedianPointSize <= 0 ||
            anchor.MedianPointSize <= 0)
        {
            return false;
        }

        var pointSizeRatio =
            marker.MedianPointSize /
            anchor.MedianPointSize;

        if (pointSizeRatio <
                MinimumMarkerToAnchorPointSizeRatio ||
            pointSizeRatio >
                MaximumMarkerToAnchorPointSizeRatio)
        {
            return false;
        }

        var horizontalGap =
            marker.Left -
            anchor.Right;

        if (Math.Abs(
                horizontalGap) >
            anchor.MedianPointSize *
            MaximumHorizontalGapToAnchorPointSizeRatio)
        {
            return false;
        }

        var verticalRise =
            marker.CenterY -
            anchor.CenterY;

        return verticalRise >=
                   anchor.MedianPointSize *
                   MinimumVerticalRiseToAnchorPointSizeRatio &&
               verticalRise <=
                   anchor.MedianPointSize *
                   MaximumVerticalRiseToAnchorPointSizeRatio;
    }

    private static bool IsRaisedNumericMarkerText(
        string text) =>
        text.Length is >= 1 and <= 4 &&
        text.All(
            char.IsAsciiDigit);

    private static PdfInlineWordGeometry? ToGeometry(
        Word word)
    {
        var medianPointSize =
            GetMedianPointSize(
                word);

        if (medianPointSize is null)
        {
            return null;
        }

        var bounds =
            word.BoundingBox;

        var xs =
            new[]
            {
                bounds.BottomLeft.X,
                bounds.BottomRight.X,
                bounds.TopLeft.X,
                bounds.TopRight.X
            };

        var ys =
            new[]
            {
                bounds.BottomLeft.Y,
                bounds.BottomRight.Y,
                bounds.TopLeft.Y,
                bounds.TopRight.Y
            };

        var left =
            xs.Min();

        var right =
            xs.Max();

        var bottom =
            ys.Min();

        var top =
            ys.Max();

        return new PdfInlineWordGeometry(
            left,
            right,
            bottom,
            top,
            (bottom + top) / 2.0,
            medianPointSize.Value);
    }

    private static double? GetMedianPointSize(
        Word word)
    {
        var pointSizes =
            word.Letters
                .Select(letter =>
                    letter.PointSize)
                .Where(pointSize =>
                    pointSize > 0 &&
                    double.IsFinite(
                        pointSize))
                .OrderBy(pointSize =>
                    pointSize)
                .ToArray();

        if (pointSizes.Length == 0)
        {
            return null;
        }

        var middle =
            pointSizes.Length / 2;

        return pointSizes.Length % 2 == 0
            ? (pointSizes[middle - 1] +
               pointSizes[middle]) / 2.0
            : pointSizes[middle];
    }

    private static string[] SplitSourceTextLines(
        string text) =>
        text
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                '\r',
                '\n')
            .Split(
                '\n');

    #endregion
}

internal readonly record struct PdfInlineWordGeometry(
    double Left,
    double Right,
    double Bottom,
    double Top,
    double CenterY,
    double MedianPointSize);

internal sealed record PdfInlineTextBlockReconstruction(
    string Text,
    IReadOnlyList<Word> Words);
