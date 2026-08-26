using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Pdf;

/// <summary>
/// Repairs narrowly evidenced PdfPig page-local text fragmentation.
///
/// This is PDF-native reconstruction only. It does not classify footnotes or
/// infer document semantics. DocumentWord.SourceSequence is never changed.
/// When several PdfPig blocks are reconstructed as one logical block, the
/// earliest contributing SourceSequence is retained as the stable block
/// provenance anchor.
/// </summary>
internal static class PdfPageNativeTextRepair
{
    #region Variables and Constants

    private const double MinimumMarkerToAnchorPointSizeRatio = 0.65;
    private const double MaximumMarkerToAnchorPointSizeRatio = 0.86;
    private const double MaximumHorizontalGapFloor = 0.0015;
    private const double MaximumHorizontalGapToAnchorHeightRatio = 0.18;
    private const double MinimumVerticalRiseToReferenceHeightRatio = 0.20;
    private const double MaximumVerticalRiseToReferenceHeightRatio = 0.70;
    private const double ReferenceHeightPercentile = 0.75;
    private const double SamePointSizeToleranceRatio = 0.05;
    private const double LineToleranceToReferenceHeightRatio = 0.45;
    private const double MinimumLineTolerance = 0.0010;

    #endregion


    #region Methods

    public static IReadOnlyList<DocumentTextBlock> Reconstruct(
        IReadOnlyList<DocumentTextBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        if (blocks.Count == 0)
        {
            return blocks;
        }

        var working =
            blocks.ToArray();

        var relations =
            FindRelations(
                working);

        var unresolved =
            relations
                .Where(relation =>
                    !relation.IsAlreadyAdjacent)
                .ToArray();

        if (unresolved.Length == 0)
        {
            return working;
        }

        var parent =
            Enumerable
                .Range(0, working.Length)
                .ToArray();

        foreach (var relation in unresolved)
        {
            Union(
                parent,
                relation.Marker.BlockIndex,
                relation.Anchor.BlockIndex);

            if (relation.TrailingPunctuation is { } trailingPunctuation)
            {
                Union(
                    parent,
                    relation.Marker.BlockIndex,
                    trailingPunctuation.BlockIndex);
            }
        }

        var roots =
            unresolved
                .Select(relation =>
                    Find(
                        parent,
                        relation.Marker.BlockIndex))
                .ToHashSet();

        var membersByRoot =
            roots.ToDictionary(
                root => root,
                _ => new List<int>());

        for (var index = 0;
             index < working.Length;
             index++)
        {
            var root =
                Find(
                    parent,
                    index);

            if (membersByRoot.TryGetValue(root, out var members))
            {
                members.Add(index);
            }
        }

        var replacementByFirst =
            new Dictionary<int, DocumentTextBlock>();

        var consumed =
            new HashSet<int>();

        foreach (var members in membersByRoot.Values)
        {
            members.Sort();

            replacementByFirst.Add(
                members[0],
                ReconstructComponent(
                    working,
                    members,
                    relations));

            foreach (var member in members)
            {
                consumed.Add(member);
            }
        }

        var output =
            new List<DocumentTextBlock>();

        for (var index = 0;
             index < working.Length;
             index++)
        {
            if (replacementByFirst.TryGetValue(index, out var replacement))
            {
                output.Add(replacement);
                continue;
            }

            if (!consumed.Contains(index))
            {
                output.Add(working[index]);
            }
        }

        return output;
    }

    private static IReadOnlyList<Relation> FindRelations(
        IReadOnlyList<DocumentTextBlock> blocks)
    {
        var positions =
            blocks
                .SelectMany(
                    (block, blockIndex) =>
                        block.Words.Select(
                            (word, wordIndex) =>
                                new WordPosition(
                                    blockIndex,
                                    wordIndex,
                                    word)))
                .ToArray();

        var relations =
            new List<Relation>();

        foreach (var marker in positions)
        {
            if (!IsNumeric(marker.Word.Text) ||
                marker.Word.MedianPointSize is null)
            {
                continue;
            }

            var anchors =
                positions
                    .Where(candidate =>
                        !ReferenceEquals(
                            candidate.Word,
                            marker.Word) &&
                        IsCompatible(
                            marker.Word,
                            candidate.Word,
                            blocks[
                                marker.BlockIndex],
                            blocks[
                                candidate.BlockIndex]))
                    .ToArray();

            if (anchors.Length != 1)
            {
                continue;
            }

            var anchor =
                anchors[0];

            var trailingPunctuationCandidates =
                positions
                    .Where(candidate =>
                        IsCompatibleTrailingPunctuation(
                            marker,
                            anchor,
                            candidate,
                            blocks))
                    .ToArray();

            if (trailingPunctuationCandidates.Length > 1)
            {
                continue;
            }

            var trailingPunctuation =
                trailingPunctuationCandidates
                    .SingleOrDefault();

            relations.Add(
                new Relation(
                    marker,
                    anchor,
                    trailingPunctuation,
                    marker.BlockIndex ==
                        anchor.BlockIndex &&
                    marker.WordIndex ==
                        anchor.WordIndex + 1 &&
                    trailingPunctuation is null));
        }

        return relations;
    }

    private static bool IsCompatible(
        DocumentWord marker,
        DocumentWord anchor,
        DocumentTextBlock markerBlock,
        DocumentTextBlock anchorBlock)
    {
        if (marker.MedianPointSize is null ||
            anchor.MedianPointSize is null ||
            IsNumeric(anchor.Text) ||
            !anchor.Text.Any(char.IsLetterOrDigit))
        {
            return false;
        }

        var sizeRatio =
            marker.MedianPointSize.Value /
            anchor.MedianPointSize.Value;

        if (sizeRatio <
                MinimumMarkerToAnchorPointSizeRatio ||
            sizeRatio >
                MaximumMarkerToAnchorPointSizeRatio)
        {
            return false;
        }

        var referenceHeight =
            GetLocalReferenceHeight(
                markerBlock,
                anchorBlock,
                anchor);

        if (referenceHeight <= 0)
        {
            return false;
        }

        var dx =
            marker.Bounds.Left -
            anchor.Bounds.Right;

        var maxDx =
            Math.Max(
                MaximumHorizontalGapFloor,
                referenceHeight *
                MaximumHorizontalGapToAnchorHeightRatio);

        if (Math.Abs(dx) > maxDx)
        {
            return false;
        }

        var rise =
            CenterY(anchor) -
            CenterY(marker);

        return rise >=
                   referenceHeight *
                   MinimumVerticalRiseToReferenceHeightRatio &&
               rise <=
                   referenceHeight *
                   MaximumVerticalRiseToReferenceHeightRatio;
    }

    private static bool IsCompatibleTrailingPunctuation(
        WordPosition marker,
        WordPosition anchor,
        WordPosition candidate,
        IReadOnlyList<DocumentTextBlock> blocks)
    {
        if (candidate.BlockIndex == marker.BlockIndex ||
            blocks[candidate.BlockIndex].WordCount != 1 ||
            candidate.Word.SourceSequence !=
                marker.Word.SourceSequence + 1 ||
            candidate.Word.Text.Length is not >= 1 and <= 3 ||
            !candidate.Word.Text.All(char.IsPunctuation) ||
            candidate.Word.MedianPointSize is null ||
            anchor.Word.MedianPointSize is null)
        {
            return false;
        }

        var pointSizeDelta =
            Math.Abs(
                candidate.Word.MedianPointSize.Value -
                anchor.Word.MedianPointSize.Value) /
            anchor.Word.MedianPointSize.Value;

        if (pointSizeDelta > SamePointSizeToleranceRatio)
        {
            return false;
        }

        var referenceHeight =
            GetLocalReferenceHeight(
                blocks[marker.BlockIndex],
                blocks[anchor.BlockIndex],
                anchor.Word);

        if (referenceHeight <= 0)
        {
            return false;
        }

        var horizontalGap =
            candidate.Word.Bounds.Left -
            marker.Word.Bounds.Right;

        var maximumHorizontalGap =
            Math.Max(
                MaximumHorizontalGapFloor,
                referenceHeight *
                MaximumHorizontalGapToAnchorHeightRatio);

        var lineTolerance =
            Math.Max(
                MinimumLineTolerance,
                referenceHeight *
                LineToleranceToReferenceHeightRatio);

        return Math.Abs(horizontalGap) <=
                   maximumHorizontalGap &&
               Math.Abs(
                   CenterY(candidate.Word) -
                   CenterY(anchor.Word)) <=
               lineTolerance;
    }

    private static DocumentTextBlock ReconstructComponent(
        IReadOnlyList<DocumentTextBlock> blocks,
        IReadOnlyList<int> members,
        IReadOnlyList<Relation> relations)
    {
        var memberSet =
            members.ToHashSet();

        var primary =
            members
                .Select(index =>
                    blocks[index])
                .OrderByDescending(block =>
                    block.WordCount)
                .ThenBy(block =>
                    block.SourceSequence)
                .First();

        var words =
            members
                .SelectMany(index =>
                    blocks[index].Words)
                .ToArray();

        var markerAnchors =
            relations
                .Where(relation =>
                    memberSet.Contains(
                        relation.Marker.BlockIndex) &&
                    memberSet.Contains(
                        relation.Anchor.BlockIndex))
                .ToDictionary(
                    relation =>
                        relation.Marker.Word,
                    relation =>
                        relation.Anchor.Word);

        var lines =
            BuildLines(
                words,
                markerAnchors);

        var repairedWords =
            lines
                .SelectMany(line =>
                    line.Words)
                .ToArray();

        var sourceSequence =
            members.Min(index =>
                blocks[index].SourceSequence);

        var readingOrders =
            members
                .Select(index =>
                    blocks[index].ReadingOrder)
                .OfType<int>()
                .ToArray();

        return new DocumentTextBlock(
            sourceSequence,
            readingOrders.Length == 0
                ? null
                : readingOrders.Min(),
            string.Join(
                "\n",
                lines.Select(line =>
                    string.Join(
                        " ",
                        line.Words.Select(word =>
                            word.Text)))),
            new NormalizedRectangle(
                repairedWords.Min(word =>
                    word.Bounds.Left),
                repairedWords.Min(word =>
                    word.Bounds.Top),
                repairedWords.Max(word =>
                    word.Bounds.Right),
                repairedWords.Max(word =>
                    word.Bounds.Bottom)),
            repairedWords,
            primary.DominantFontName,
            primary.MedianPointSize,
            members.Sum(index =>
                blocks[index].LineCount));
    }

    private static IReadOnlyList<Line> BuildLines(
        IReadOnlyList<DocumentWord> words,
        IReadOnlyDictionary<DocumentWord, DocumentWord> markerAnchors)
    {
        var referenceHeight =
            GetComponentReferenceHeight(
                words);

        var tolerance =
            Math.Max(
                MinimumLineTolerance,
                referenceHeight *
                LineToleranceToReferenceHeightRatio);

        var positioned =
            words
                .Select(word =>
                    new PositionedWord(
                        word,
                        markerAnchors.TryGetValue(word, out var anchor)
                            ? CenterY(anchor)
                            : CenterY(word)))
                .OrderBy(item =>
                    item.CenterY)
                .ThenBy(item =>
                    item.Word.Bounds.Left)
                .ToArray();

        var groups =
            new List<MutableLine>();

        foreach (var item in positioned)
        {
            if (groups.Count == 0 ||
                Math.Abs(
                    item.CenterY -
                    groups[^1].CenterY) >
                tolerance)
            {
                groups.Add(
                    new MutableLine(
                        item.CenterY));

                groups[^1].Words.Add(
                    item.Word);

                continue;
            }

            var group =
                groups[^1];

            group.Words.Add(
                item.Word);

            group.CenterY =
                (
                    group.CenterY *
                    (group.Words.Count - 1) +
                    item.CenterY
                ) /
                group.Words.Count;
        }

        return groups
            .Select(group =>
                new Line(
                    group.CenterY,
                    group.Words
                        .OrderBy(word =>
                            word.Bounds.Left)
                        .ThenBy(word =>
                            word.SourceSequence)
                        .ToArray()))
            .OrderBy(line =>
                line.CenterY)
            .ToArray();
    }

    private static double GetLocalReferenceHeight(
        DocumentTextBlock markerBlock,
        DocumentTextBlock anchorBlock,
        DocumentWord anchor)
    {
        if (anchor.MedianPointSize is null)
        {
            return Height(
                anchor);
        }

        var heights =
            anchorBlock.Words
                .Where(word =>
                    word.MedianPointSize is not null &&
                    !IsNumeric(
                        word.Text) &&
                    word.Text.Any(
                        char.IsLetterOrDigit) &&
                    Math.Abs(
                        word.MedianPointSize.Value -
                        anchor.MedianPointSize.Value) /
                        anchor.MedianPointSize.Value <=
                    SamePointSizeToleranceRatio)
                .Select(
                    Height)
                .Where(height =>
                    height > 0)
                .OrderBy(height =>
                    height)
                .ToArray();

        if (heights.Length < 3)
        {
            heights =
                markerBlock.Words
                    .Where(word =>
                        word.MedianPointSize is not null &&
                        !IsNumeric(
                            word.Text) &&
                        word.Text.Any(
                            char.IsLetterOrDigit) &&
                        Math.Abs(
                            word.MedianPointSize.Value -
                            anchor.MedianPointSize.Value) /
                            anchor.MedianPointSize.Value <=
                        SamePointSizeToleranceRatio)
                    .Select(
                        Height)
                    .Where(height =>
                        height > 0)
                    .OrderBy(height =>
                        height)
                    .ToArray();
        }

        if (heights.Length < 3)
        {
            return Height(
                anchor);
        }

        return Math.Max(
            Height(
                anchor),
            Percentile(
                heights,
                ReferenceHeightPercentile));
    }

    private static double GetComponentReferenceHeight(
        IReadOnlyList<DocumentWord> words)
    {
        var bodyPointSizes =
            words
                .Where(word =>
                    word.MedianPointSize is not null &&
                    !IsNumeric(
                        word.Text) &&
                    word.Text.Any(
                        char.IsLetterOrDigit))
                .Select(word =>
                    word.MedianPointSize!.Value)
                .ToArray();

        if (bodyPointSizes.Length == 0)
        {
            return 0;
        }

        var referenceBodyPointSize =
            Median(
                bodyPointSizes
                    .OrderBy(pointSize =>
                        pointSize)
                    .ToArray());

        var heights =
            words
                .Where(word =>
                    word.MedianPointSize is not null &&
                    !IsNumeric(
                        word.Text) &&
                    word.Text.Any(
                        char.IsLetterOrDigit) &&
                    Math.Abs(
                        word.MedianPointSize.Value -
                        referenceBodyPointSize) /
                        referenceBodyPointSize <=
                    SamePointSizeToleranceRatio)
                .Select(
                    Height)
                .Where(height =>
                    height > 0)
                .OrderBy(height =>
                    height)
                .ToArray();

        return heights.Length == 0
            ? 0
            : Percentile(
                heights,
                ReferenceHeightPercentile);
    }

    private static double Height(
        DocumentWord word) =>
        word.Bounds.Bottom -
        word.Bounds.Top;

    private static double Percentile(
        IReadOnlyList<double> sorted,
        double percentile)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        if (sorted.Count == 1)
        {
            return sorted[0];
        }

        var position =
            (
                sorted.Count -
                1
            ) *
            percentile;

        var lower =
            (int)Math.Floor(
                position);

        var upper =
            (int)Math.Ceiling(
                position);

        if (lower ==
            upper)
        {
            return sorted[
                lower];
        }

        var weight =
            position -
            lower;

        return sorted[lower] *
               (
                   1 -
                   weight
               ) +
               sorted[upper] *
               weight;
    }

    private static bool IsNumeric(
        string text) =>
        text.Length is >= 1 and <= 4 &&
        text.All(char.IsAsciiDigit);

    private static double CenterY(
        DocumentWord word) =>
        (
            word.Bounds.Top +
            word.Bounds.Bottom
        ) / 2.0;

    private static double Median(
        IReadOnlyList<double> sorted)
    {
        var middle =
            sorted.Count / 2;

        return sorted.Count % 2 == 0
            ? (
                sorted[middle - 1] +
                sorted[middle]
              ) / 2.0
            : sorted[middle];
    }

    private static int Find(
        int[] parent,
        int value)
    {
        while (parent[value] != value)
        {
            parent[value] =
                parent[parent[value]];

            value =
                parent[value];
        }

        return value;
    }

    private static void Union(
        int[] parent,
        int left,
        int right)
    {
        var leftRoot =
            Find(parent, left);

        var rightRoot =
            Find(parent, right);

        if (leftRoot != rightRoot)
        {
            parent[rightRoot] =
                leftRoot;
        }
    }

    #endregion


    private sealed record WordPosition(
        int BlockIndex,
        int WordIndex,
        DocumentWord Word);

    private sealed record Relation(
        WordPosition Marker,
        WordPosition Anchor,
        WordPosition? TrailingPunctuation,
        bool IsAlreadyAdjacent);

    private sealed record PositionedWord(
        DocumentWord Word,
        double CenterY);

    private sealed record Line(
        double CenterY,
        IReadOnlyList<DocumentWord> Words);

    private sealed class MutableLine
    {
        #region Properties

        public double CenterY { get; set; }

        public List<DocumentWord> Words { get; } =
            [];

        #endregion


        #region ctor

        public MutableLine(
            double centerY)
        {
            CenterY =
                centerY;
        }

        #endregion
    }
}
