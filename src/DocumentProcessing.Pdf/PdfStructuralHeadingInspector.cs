using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace DocumentProcessing.Pdf;

/// <summary>
/// Derives conservative structural-heading observations from native PDF text
/// and typography without rasterization or OCR.
/// </summary>
internal static class PdfStructuralHeadingInspector
{
    #region Variables and Constants

    private const double MinimumHeadingToBodyPointSizeRatio =
        1.25;

    private const double PointSizeLevelToleranceRatio =
        0.08;

    private const int MaximumHeadingCharacters =
        160;

    private const int MaximumHeadingWords =
        20;

    private const int MinimumRecurringHeaderPages =
        3;

    private static readonly NearestNeighbourWordExtractor DeterministicWordExtractor =
        new(
            new NearestNeighbourWordExtractor
                .NearestNeighbourWordExtractorOptions
            {
                MaxDegreeOfParallelism =
                    1,
                GroupByOrientation =
                    true
            });

    #endregion

    #region Methods

    public static StructuralHeadingInspection Inspect(
        PdfDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        var lines =
            new List<ObservedBlock>();

        var sourceOrder =
            0;

        for (var physicalPageNumber = 1;
             physicalPageNumber <= document.NumberOfPages;
             physicalPageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page =
                document.GetPage(
                    physicalPageNumber);

            var coordinateSpace =
                PdfPageCoordinateSpace.Create(
                    page);

            var words =
                page
                    .GetWords(
                        DeterministicWordExtractor)
                    .Where(
                        word =>
                            !string.IsNullOrWhiteSpace(
                                word.Text))
                    .Select(
                        word =>
                            new ObservedWord(
                                word.Text,
                                coordinateSpace.ToNormalizedRectangle(
                                    word.BoundingBox),
                                GetMedianPointSize(
                                    word.Letters)))
                    .ToArray();

            lines.AddRange(
                BuildLines(
                    words,
                    physicalPageNumber,
                    ref sourceOrder));
        }

        var entries =
            BuildEntries(
                lines);

        return new StructuralHeadingInspection(
            DocumentFormatId.Pdf,
            new DocumentStructureAxis.PhysicalPages(
                document.NumberOfPages),
            entries);
    }

    private static IReadOnlyList<StructuralHeadingEntry> BuildEntries(
        IReadOnlyList<ObservedBlock> blocks)
    {
        var bodyPointSize =
            WeightedMedianPointSize(
                blocks);

        if (bodyPointSize is null)
        {
            return [];
        }

        var repeatedTexts =
            blocks
                .GroupBy(
                    block =>
                        block.Title,
                    StringComparer.OrdinalIgnoreCase)
                .Where(
                    group =>
                        group
                            .Select(
                                block =>
                                    block.PhysicalPageNumber)
                            .Distinct()
                            .Count() >=
                        MinimumRecurringHeaderPages)
                .Select(
                    group =>
                        group.Key)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        var candidates =
            blocks
                .Where(
                    block =>
                        block.PointSize >=
                        bodyPointSize.Value *
                        MinimumHeadingToBodyPointSizeRatio &&
                        block.Title.Length <=
                        MaximumHeadingCharacters &&
                        block.WordCount is >
                            0 and <=
                            MaximumHeadingWords &&
                        block.Title.Any(
                            char.IsLetter) &&
                        !repeatedTexts.Contains(
                            block.Title))
                .ToArray();

        if (candidates.Length ==
            0)
        {
            return [];
        }

        var pointSizeLevels =
            BuildPointSizeLevels(
                candidates);

        return candidates
            .OrderBy(
                candidate =>
                    candidate.SourceOrder)
            .Select(
                candidate =>
                    new StructuralHeadingEntry(
                        candidate.Title,
                        FindHierarchyLevel(
                            candidate.PointSize,
                            pointSizeLevels),
                        candidate.SourceOrder,
                        new DocumentStructurePosition.PhysicalPage(
                            candidate.PhysicalPageNumber)))
            .ToArray();
    }

    private static double? WeightedMedianPointSize(
        IReadOnlyList<ObservedBlock> blocks)
    {
        if (blocks.Count ==
            0)
        {
            return null;
        }

        var ordered =
            blocks
                .OrderBy(
                    block =>
                        block.PointSize)
                .ToArray();

        var totalWeight =
            ordered.Sum(
                block =>
                    Math.Max(
                        block.WordCount,
                        1));

        var targetWeight =
            (totalWeight +
             1) /
            2;

        var cumulativeWeight =
            0;

        foreach (var block in
                 ordered)
        {
            cumulativeWeight +=
                Math.Max(
                    block.WordCount,
                    1);

            if (cumulativeWeight >=
                targetWeight)
            {
                return block.PointSize;
            }
        }

        return ordered[^1].PointSize;
    }

    private static IReadOnlyList<ObservedBlock> BuildLines(
        IReadOnlyList<ObservedWord> words,
        int physicalPageNumber,
        ref int sourceOrder)
    {
        var lines =
            new List<MutableLine>();

        foreach (var word in
                 words
                     .Where(
                         word =>
                             word.MedianPointSize is >
                             0)
                     .OrderBy(
                         word =>
                             VerticalCenter(
                                 word.Bounds))
                     .ThenBy(
                         word =>
                             word.Bounds.Left))
        {
            var center =
                VerticalCenter(
                    word.Bounds);

            var height =
                Math.Max(
                    word.Bounds.Bottom -
                    word.Bounds.Top,
                    0.0001);

            var line =
                lines.FirstOrDefault(
                    candidate =>
                        Math.Abs(
                            candidate.Center -
                            center) <=
                        Math.Max(
                            candidate.Height,
                            height) *
                        0.45);

            if (line is null)
            {
                line =
                    new MutableLine(
                        center,
                        height);

                lines.Add(
                    line);
            }

            line.Words.Add(
                word);
        }

        var observed =
            new List<ObservedBlock>(
                lines.Count);

        foreach (var line in
                 lines
                     .OrderBy(
                         candidate =>
                             candidate.Center)
                     .ThenBy(
                         candidate =>
                             candidate.Words.Min(
                                 word =>
                                     word.Bounds.Left)))
        {
            var orderedWords =
                line.Words
                    .OrderBy(
                        word =>
                            word.Bounds.Left)
                    .ToArray();

            var title =
                NormalizeTitle(
                    string.Join(
                        ' ',
                        orderedWords.Select(
                            word =>
                                word.Text)));

            if (title is null)
            {
                continue;
            }

            var pointSizes =
                orderedWords
                    .Select(
                        word =>
                            word.MedianPointSize!.Value)
                    .OrderBy(
                        pointSize =>
                            pointSize)
                    .ToArray();

            observed.Add(
                new ObservedBlock(
                    physicalPageNumber,
                    sourceOrder++,
                    title,
                    pointSizes[pointSizes.Length /
                               2],
                    orderedWords.Length));
        }

        return observed;
    }

    private static double VerticalCenter(
        NormalizedRectangle bounds) =>
        (bounds.Top +
         bounds.Bottom) /
        2.0;

    private static double? GetMedianPointSize(
        IReadOnlyCollection<Letter> letters)
    {
        var pointSizes =
            letters
                .Select(
                    letter =>
                        letter.PointSize)
                .Where(
                    pointSize =>
                        pointSize >
                        0 &&
                        double.IsFinite(
                            pointSize))
                .OrderBy(
                    pointSize =>
                        pointSize)
                .ToArray();

        if (pointSizes.Length ==
            0)
        {
            return null;
        }

        var middle =
            pointSizes.Length /
            2;

        return pointSizes.Length %
               2 ==
               0
            ? (pointSizes[middle -
                          1] +
               pointSizes[middle]) /
              2.0
            : pointSizes[middle];
    }

    private static IReadOnlyList<double> BuildPointSizeLevels(
        IReadOnlyList<ObservedBlock> candidates)
    {
        var levels =
            new List<double>();

        foreach (var pointSize in
                 candidates
                     .Select(
                         candidate =>
                             candidate.PointSize)
                     .OrderByDescending(
                         value =>
                             value))
        {
            if (levels.Any(
                    level =>
                        IsSamePointSizeLevel(
                            pointSize,
                            level)))
            {
                continue;
            }

            levels.Add(
                pointSize);
        }

        return levels;
    }

    private static int FindHierarchyLevel(
        double pointSize,
        IReadOnlyList<double> levels)
    {
        for (var index = 0;
             index < levels.Count;
             index++)
        {
            if (IsSamePointSizeLevel(
                    pointSize,
                    levels[index]))
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            "The PDF heading point size was not assigned to a hierarchy level.");
    }

    private static bool IsSamePointSizeLevel(
        double first,
        double second) =>
        Math.Abs(
            first -
            second) /
        Math.Max(
            first,
            second) <=
        PointSizeLevelToleranceRatio;

    private static string? NormalizeTitle(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        return string.Join(
            ' ',
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record ObservedBlock(
        int PhysicalPageNumber,
        int SourceOrder,
        string Title,
        double PointSize,
        int WordCount);

    private sealed record ObservedWord(
        string Text,
        NormalizedRectangle Bounds,
        double? MedianPointSize);

    private sealed record MutableLine(
        double Center,
        double Height)
    {
        public List<ObservedWord> Words { get; } =
            [];
    }

    #endregion
}
