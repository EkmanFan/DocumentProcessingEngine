using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Pdf.Notes;

/// <summary>
/// Concludes numeric bottom-of-page note relations from PDF-native text
/// evidence.
///
/// The analyzer consumes source geometry, typography and source sequence
/// produced by PDF extraction. It does not decide how concluded notes affect
/// the main reading flow, segmentation, quality or portable projection.
///
/// Ambiguous evidence fails closed: unmatched numbers, duplicate labels,
/// missing typography and mixed body/note source blocks remain ordinary text.
/// </summary>
internal sealed class PdfBottomOfPageNoteAnalyzer
    : IPdfDocumentNoteStrategy
{
    #region Variables and Constants

    private const double MinimumNotePointSizeRatio =
        0.90;

    private const double MaximumNotePointSizeRatio =
        1.10;

    private const double MinimumVisualLineTolerance =
        0.0010;

    private const double VisualLineHeightToleranceRatio =
        0.45;

    #endregion


    #region Methods Analysis

    public IReadOnlyList<PagedNativeDocumentNote> Analyze(
        DocumentExtractionResult extraction,
        CancellationToken cancellationToken = default)
        => Analyze(
            extraction,
            claimedReferences:
                new HashSet<PdfNativeNoteReferenceKey>(),
            cancellationToken);

    public IReadOnlyList<PagedNativeDocumentNote> Analyze(
        DocumentExtractionResult extraction,
        IReadOnlySet<PdfNativeNoteReferenceKey> claimedReferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            claimedReferences);

        cancellationToken.ThrowIfCancellationRequested();

        var pages =
            extraction.Pages
                .Select(
                    page =>
                        new PagedNativeNotePageEvidence(
                            page.PhysicalPageNumber,
                            page.Blocks))
                .ToArray();

        return AnalyzeEvidence(
                pages,
                cancellationToken)
            .Where(
                note =>
                    note.References.All(
                        reference =>
                            !claimedReferences.Contains(
                                PdfNativeNoteReferenceKey.From(
                                    reference))))
            .ToArray();
    }

    internal static IReadOnlyList<PagedNativeDocumentNote> AnalyzeEvidence(
        IReadOnlyList<PagedNativeNotePageEvidence> pages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            pages);

        cancellationToken.ThrowIfCancellationRequested();

        ValidatePages(
            pages);

        var pageAnalyses =
            pages
                .Select(
                    page =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        return AnalyzePage(
                            page);
                    })
                .ToArray();

        AttachCrossPageContinuations(
            pageAnalyses,
            cancellationToken);

        var entries =
            pageAnalyses
                .SelectMany(
                    page =>
                        page.Entries)
                .OrderBy(
                    entry =>
                        entry.FirstPhysicalPageNumber)
                .ThenBy(
                    entry =>
                        entry.FirstLineCenterY)
                .Select(
                    entry =>
                        entry.ToTopology())
                .ToArray();

        return Array.AsReadOnly(
            entries);
    }

    private static PageAnalysis AnalyzePage(
        PagedNativeNotePageEvidence page)
    {
        var references =
            FindRaisedReferences(
                page);

        var referencesByValue =
            references
                .GroupBy(
                    reference =>
                        reference.Value,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.ToArray(),
                    StringComparer.Ordinal);

        var standaloneLabels =
            FindStandaloneLabels(
                page)
                .Where(
                    label =>
                        referencesByValue.ContainsKey(
                            label.Value))
                .ToArray();

        var mappedLabels =
            new List<MappedLabel>();

        foreach (var group in
                 standaloneLabels
                     .GroupBy(
                         label =>
                             label.Value,
                         StringComparer.Ordinal))
        {
            if (group.Count() !=
                1)
            {
                continue;
            }

            var candidate =
                group.Single();

            var matches =
                candidate.Block.Words
                    .Where(
                        word =>
                            string.Equals(
                                word.Text,
                                candidate.Value,
                                StringComparison.Ordinal))
                    .ToArray();

            if (matches.Length !=
                    1 ||
                matches[0].MedianPointSize is null)
            {
                continue;
            }

            mappedLabels.Add(
                new MappedLabel(
                    candidate.Value,
                    candidate.Block,
                    matches[0]));
        }

        var notePointSizes =
            mappedLabels
                .Select(
                    label =>
                        label.Word.MedianPointSize!.Value)
                .Where(
                    value =>
                        value >
                            0 &&
                        double.IsFinite(
                            value))
                .OrderBy(
                    value =>
                        value)
                .ToArray();

        var notePointSize =
            Median(
                notePointSizes);

        if (notePointSize is null)
        {
            return PageAnalysis.Empty(
                page.PhysicalPageNumber,
                references);
        }

        var noteSizedWords =
            page.Blocks
                .SelectMany(
                    block =>
                        block.Words
                            .Where(
                                word =>
                                    IsNoteSized(
                                        word,
                                        notePointSize.Value))
                            .Select(
                                word =>
                                    new LocatedWord(
                                        block.SourceSequence,
                                        word)))
                .ToArray();

        var visualLines =
            BuildVisualLines(
                noteSizedWords);

        var lineByWordSourceSequence =
            BuildLineIndex(
                visualLines);

        var labelsOnLines =
            mappedLabels
                .Select(
                    label =>
                        lineByWordSourceSequence.TryGetValue(
                            label.Word.SourceSequence,
                            out var lineIndex)
                            ? new LabelOnLine(
                                label,
                                lineIndex)
                            : null)
                .Where(
                    label =>
                        label is not null)
                .Select(
                    label =>
                        label!)
                .OrderBy(
                    label =>
                        label.LineIndex)
                .ThenBy(
                    label =>
                        label.Label.Word.Bounds.Left)
                .ToArray();

        var ambiguousLines =
            labelsOnLines
                .GroupBy(
                    label =>
                        label.LineIndex)
                .Where(
                    group =>
                        group.Count() >
                            1)
                .Select(
                    group =>
                        group.Key)
                .ToHashSet();

        var raisedReferenceBlocks =
            references
                .Select(
                    reference =>
                        reference.SourceBlockSequence)
                .ToHashSet();

        var entries =
            new List<EntryBuilder>();

        for (var index = 0;
             index <
             labelsOnLines.Length;
             index++)
        {
            var current =
                labelsOnLines[index];

            if (ambiguousLines.Contains(
                    current.LineIndex))
            {
                continue;
            }

            var nextLineIndex =
                index + 1 <
                labelsOnLines.Length
                    ? labelsOnLines[index + 1].LineIndex
                    : visualLines.Count;

            if (nextLineIndex <=
                current.LineIndex)
            {
                continue;
            }

            var payloadLines =
                new List<PagedNativeNotePayloadLine>();

            var sourceBlocks =
                new HashSet<PagedNativeNoteSourceBlock>
                {
                    new(
                        page.PhysicalPageNumber,
                        current.Label.Block.SourceSequence)
                };

            for (var lineIndex =
                     current.LineIndex;
                 lineIndex <
                     nextLineIndex;
                 lineIndex++)
            {
                var line =
                    visualLines[lineIndex];

                var payloadWords =
                    lineIndex ==
                            current.LineIndex
                        ? line.Words
                            .Where(
                                item =>
                                    item.Word.SourceSequence !=
                                    current.Label.Word.SourceSequence)
                            .ToArray()
                        : line.Words
                            .ToArray();

                if (payloadWords.Length ==
                    0)
                {
                    continue;
                }

                var payloadLine =
                    ToPayloadLine(
                        page.PhysicalPageNumber,
                        payloadWords);

                payloadLines.Add(
                    payloadLine);

                foreach (var sourceSequence in
                         payloadLine.SourceBlockSequences)
                {
                    sourceBlocks.Add(
                        new PagedNativeNoteSourceBlock(
                            page.PhysicalPageNumber,
                            sourceSequence));
                }
            }

            if (payloadLines.Count ==
                0)
            {
                continue;
            }

            if (sourceBlocks.Any(
                    block =>
                        raisedReferenceBlocks.Contains(
                            block.SourceSequence)))
            {
                // A source block containing both correlated body-reference
                // evidence and candidate note payload is ambiguous at current
                // V1 granularity. Preserve it in ordinary text flow.
                continue;
            }

            var correlatedReferences =
                referencesByValue[
                        current.Label.Value]
                    .Select(
                        reference =>
                            new PagedNativeNoteReference(
                                current.Label.Value,
                                page.PhysicalPageNumber,
                                reference.SourceBlockSequence,
                                reference.Word.SourceSequence,
                                reference.Word.Bounds))
                    .ToArray();

            entries.Add(
                new EntryBuilder(
                    current.Label.Value,
                    correlatedReferences,
                    payloadLines,
                    sourceBlocks,
                    page.PhysicalPageNumber,
                    visualLines[
                            current.LineIndex]
                        .CenterY));
        }

        return new PageAnalysis(
            page.PhysicalPageNumber,
            references,
            visualLines,
            labelsOnLines,
            entries);
    }

    private static void AttachCrossPageContinuations(
        IReadOnlyList<PageAnalysis> pages,
        CancellationToken cancellationToken)
    {
        for (var index = 0;
             index + 1 <
             pages.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current =
                pages[index];

            var next =
                pages[index + 1];

            var lastEntry =
                current.Entries
                    .LastOrDefault();

            var firstEntry =
                next.Entries
                    .FirstOrDefault();

            if (lastEntry is null ||
                firstEntry is null ||
                !AreConsecutiveNumericLabels(
                    lastEntry.Label,
                    firstEntry.Label))
            {
                continue;
            }

            var firstLabel =
                next.LabelsOnLines
                    .FirstOrDefault(
                        label =>
                            string.Equals(
                                label.Label.Value,
                                firstEntry.Label,
                                StringComparison.Ordinal));

            if (firstLabel is null ||
                firstLabel.LineIndex <=
                    0)
            {
                continue;
            }

            var firstLabelLine =
                next.VisualLines[
                    firstLabel.LineIndex];

            var firstLabelLineBlocks =
                firstLabelLine.Words
                    .Select(
                        word =>
                            word.SourceBlockSequence)
                    .ToHashSet();

            if (firstLabelLineBlocks.Count ==
                0)
            {
                continue;
            }

            var raisedReferenceWordSequences =
                next.References
                    .Select(
                        reference =>
                            reference.Word.SourceSequence)
                    .ToHashSet();

            var prefixLines =
                next.VisualLines
                    .Take(
                        firstLabel.LineIndex)
                    .Where(
                        line =>
                            line.Words.Any(
                                word =>
                                    firstLabelLineBlocks.Contains(
                                        word.SourceBlockSequence)))
                    .ToArray();

            if (prefixLines.Length ==
                    0 ||
                prefixLines.Any(
                    line =>
                        line.Words.Any(
                            word =>
                                raisedReferenceWordSequences.Contains(
                                    word.Word.SourceSequence))))
            {
                continue;
            }

            foreach (var line in
                     prefixLines)
            {
                var payloadLine =
                    ToPayloadLine(
                        next.PhysicalPageNumber,
                        line.Words);

                lastEntry.PayloadLines.Add(
                    payloadLine);

                foreach (var sourceSequence in
                         payloadLine.SourceBlockSequences)
                {
                    lastEntry.SourceBlocks.Add(
                        new PagedNativeNoteSourceBlock(
                            next.PhysicalPageNumber,
                            sourceSequence));
                }
            }
        }
    }

    #endregion


    #region Methods Evidence Mapping

    private static IReadOnlyList<PdfRaisedNumericReferenceCandidate>
        FindRaisedReferences(
        PagedNativeNotePageEvidence page)
        => PdfRaisedNumericReferenceFinder
            .Find(
                page.PhysicalPageNumber,
                page.Blocks);

    private static IReadOnlyList<StandaloneLabelCandidate>
        FindStandaloneLabels(
        PagedNativeNotePageEvidence page)
    {
        var labels =
            new List<StandaloneLabelCandidate>();

        foreach (var block in
                 page.Blocks)
        {
            foreach (var line in
                     SplitLines(
                         block.Text))
            {
                var value =
                    line.Trim();

                if (!IsNumericMarker(
                        value))
                {
                    continue;
                }

                labels.Add(
                    new StandaloneLabelCandidate(
                        value,
                        block));
            }
        }

        return labels;
    }

    #endregion


    #region Methods Visual Lines

    private static IReadOnlyList<VisualLine> BuildVisualLines(
        IReadOnlyList<LocatedWord> words)
    {
        if (words.Count ==
            0)
        {
            return [];
        }

        var heights =
            words
                .Select(
                    item =>
                        item.Word.Bounds.Bottom -
                        item.Word.Bounds.Top)
                .Where(
                    height =>
                        height >
                            0 &&
                        double.IsFinite(
                            height))
                .OrderBy(
                    height =>
                        height)
                .ToArray();

        var referenceHeight =
            Median(
                heights) ??
            0.01;

        var tolerance =
            Math.Max(
                MinimumVisualLineTolerance,
                referenceHeight *
                VisualLineHeightToleranceRatio);

        var positioned =
            words
                .Select(
                    item =>
                        new PositionedWord(
                            item,
                            CenterY(
                                item.Word)))
                .OrderBy(
                    item =>
                        item.CenterY)
                .ThenBy(
                    item =>
                        item.Word.Word.Bounds.Left)
                .ThenBy(
                    item =>
                        item.Word.Word.SourceSequence)
                .ToArray();

        var groups =
            new List<MutableVisualLine>();

        foreach (var item in
                 positioned)
        {
            var best =
                groups
                    .Select(
                        (line, lineIndex) =>
                            new
                            {
                                Line =
                                    line,
                                LineIndex =
                                    lineIndex,
                                Distance =
                                    Math.Abs(
                                        item.CenterY -
                                        line.CenterY)
                            })
                    .Where(
                        candidate =>
                            candidate.Distance <=
                            tolerance)
                    .OrderBy(
                        candidate =>
                            candidate.Distance)
                    .ThenBy(
                        candidate =>
                            candidate.LineIndex)
                    .FirstOrDefault();

            if (best is null)
            {
                var created =
                    new MutableVisualLine(
                        item.CenterY);

                created.Words.Add(
                    item.Word);

                groups.Add(
                    created);

                continue;
            }

            best.Line.Words.Add(
                item.Word);

            best.Line.CenterY =
                best.Line.Words
                    .Average(
                        word =>
                            CenterY(
                                word.Word));
        }

        return groups
            .OrderBy(
                line =>
                    line.CenterY)
            .Select(
                line =>
                    new VisualLine(
                        line.CenterY,
                        line.Words
                            .OrderBy(
                                word =>
                                    word.Word.Bounds.Left)
                            .ThenBy(
                                word =>
                                    word.Word.SourceSequence)
                            .ToArray()))
            .ToArray();
    }

    private static Dictionary<int, int> BuildLineIndex(
        IReadOnlyList<VisualLine> lines)
    {
        var result =
            new Dictionary<int, int>();

        for (var lineIndex = 0;
             lineIndex <
             lines.Count;
             lineIndex++)
        {
            foreach (var word in
                     lines[lineIndex].Words)
            {
                if (result.TryGetValue(
                        word.Word.SourceSequence,
                        out var existingLineIndex) &&
                    existingLineIndex !=
                        lineIndex)
                {
                    throw new InvalidDataException(
                        $"Native word source sequence {word.Word.SourceSequence} appears on multiple reconstructed visual lines.");
                }

                result[word.Word.SourceSequence] =
                    lineIndex;
            }
        }

        return result;
    }

    private static PagedNativeNotePayloadLine ToPayloadLine(
        int physicalPageNumber,
        IReadOnlyList<LocatedWord> words)
    {
        if (words.Count ==
            0)
        {
            throw new ArgumentException(
                "Footnote payload line cannot be empty.",
                nameof(words));
        }

        var ordered =
            words
                .OrderBy(
                    item =>
                        item.Word.Bounds.Left)
                .ThenBy(
                    item =>
                        item.Word.SourceSequence)
                .ToArray();

        return new PagedNativeNotePayloadLine(
            physicalPageNumber,
            string.Join(
                " ",
                ordered.Select(
                    item =>
                        item.Word.Text)),
            CombineBounds(
                ordered.Select(
                    item =>
                        item.Word.Bounds)),
            ordered
                .Select(
                    item =>
                        item.SourceBlockSequence)
                .Distinct()
                .OrderBy(
                    value =>
                        value)
                .ToArray(),
            ordered
                .Select(
                    item =>
                        item.Word.SourceSequence)
                .ToArray());
    }

    #endregion


    #region Methods Validation and Math

    private static void ValidatePages(
        IReadOnlyList<PagedNativeNotePageEvidence> pages)
    {
        var previousPage =
            0;

        foreach (var page in
                 pages)
        {
            if (page.PhysicalPageNumber <=
                0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pages),
                    "Physical page numbers must be positive.");
            }

            if (page.PhysicalPageNumber <=
                previousPage)
            {
                throw new ArgumentException(
                    "Footnote page evidence must preserve strict physical-page order.",
                    nameof(pages));
            }

            ArgumentNullException.ThrowIfNull(
                page.Blocks);

            previousPage =
                page.PhysicalPageNumber;
        }
    }

    private static bool IsNoteSized(
        DocumentWord word,
        double notePointSize)
    {
        if (word.MedianPointSize is null ||
            notePointSize <=
                0)
        {
            return false;
        }

        var ratio =
            word.MedianPointSize.Value /
            notePointSize;

        return ratio >=
                   MinimumNotePointSizeRatio &&
               ratio <=
                   MaximumNotePointSizeRatio;
    }

    private static bool AreConsecutiveNumericLabels(
        string current,
        string next) =>
        int.TryParse(
            current,
            out var currentNumber) &&
        int.TryParse(
            next,
            out var nextNumber) &&
        nextNumber ==
            currentNumber +
            1;

    private static bool IsNumericMarker(
        string value) =>
        value.Length is
                >= 1 and
                <= 4 &&
        value.All(
            char.IsAsciiDigit);

    private static string[] SplitLines(
        string value) =>
        value
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                '\r',
                '\n')
            .Split(
                '\n');

    private static double CenterY(
        DocumentWord word) =>
        (
            word.Bounds.Top +
            word.Bounds.Bottom
        ) /
        2.0;

    private static double? Median(
        IReadOnlyList<double> values)
    {
        if (values.Count ==
            0)
        {
            return null;
        }

        return values.Count %
                   2 ==
               0
            ? (
                values[
                    (values.Count / 2) -
                    1] +
                values[
                    values.Count / 2]
            ) /
            2.0
            : values[
                values.Count / 2];
    }

    private static NormalizedRectangle CombineBounds(
        IEnumerable<NormalizedRectangle> bounds)
    {
        var values =
            bounds.ToArray();

        if (values.Length ==
            0)
        {
            throw new ArgumentException(
                "Cannot combine an empty bounds collection.",
                nameof(bounds));
        }

        return new NormalizedRectangle(
            values.Min(
                value =>
                    value.Left),
            values.Min(
                value =>
                    value.Top),
            values.Max(
                value =>
                    value.Right),
            values.Max(
                value =>
                    value.Bottom));
    }

    #endregion


    #region Internal Types

    private sealed record StandaloneLabelCandidate(
        string Value,
        DocumentTextBlock Block);

    private sealed record MappedLabel(
        string Value,
        DocumentTextBlock Block,
        DocumentWord Word);

    private sealed record LabelOnLine(
        MappedLabel Label,
        int LineIndex);

    private sealed record LocatedWord(
        int SourceBlockSequence,
        DocumentWord Word);

    private sealed record PositionedWord(
        LocatedWord Word,
        double CenterY);

    private sealed record VisualLine(
        double CenterY,
        IReadOnlyList<LocatedWord> Words);

    private sealed class MutableVisualLine
    {
        #region Properties

        public double CenterY { get; set; }

        public List<LocatedWord> Words { get; } =
            [];

        #endregion


        #region ctor

        public MutableVisualLine(
            double centerY)
        {
            CenterY =
                centerY;
        }

        #endregion
    }

    private sealed class EntryBuilder
    {
        #region Properties

        public string Label { get; }

        public IReadOnlyList<PagedNativeNoteReference> References { get; }

        public List<PagedNativeNotePayloadLine> PayloadLines { get; }

        public HashSet<PagedNativeNoteSourceBlock> SourceBlocks { get; }

        public int FirstPhysicalPageNumber { get; }

        public double FirstLineCenterY { get; }

        #endregion


        #region ctor

        public EntryBuilder(
            string label,
            IReadOnlyList<PagedNativeNoteReference> references,
            IReadOnlyList<PagedNativeNotePayloadLine> payloadLines,
            IReadOnlyCollection<PagedNativeNoteSourceBlock> sourceBlocks,
            int firstPhysicalPageNumber,
            double firstLineCenterY)
        {
            Label =
                label;

            References =
                references;

            PayloadLines =
                payloadLines.ToList();

            SourceBlocks =
                sourceBlocks.ToHashSet();

            FirstPhysicalPageNumber =
                firstPhysicalPageNumber;

            FirstLineCenterY =
                firstLineCenterY;
        }

        #endregion


        #region Methods

        public PagedNativeDocumentNote ToTopology() =>
            new(
                Label,
                References,
                PayloadLines,
                SourceBlocks.ToArray());

        #endregion
    }

    private sealed record PageAnalysis(
        int PhysicalPageNumber,
        IReadOnlyList<PdfRaisedNumericReferenceCandidate> References,
        IReadOnlyList<VisualLine> VisualLines,
        IReadOnlyList<LabelOnLine> LabelsOnLines,
        IReadOnlyList<EntryBuilder> Entries)
    {
        public static PageAnalysis Empty(
            int physicalPageNumber,
            IReadOnlyList<PdfRaisedNumericReferenceCandidate> references) =>
            new(
                physicalPageNumber,
                references,
                [],
                [],
                []);
    }

    #endregion
}

/// <summary>
/// Minimal PDF-native page evidence accepted by the pure note classifier.
/// </summary>
internal sealed record PagedNativeNotePageEvidence(
    int PhysicalPageNumber,
    IReadOnlyList<DocumentTextBlock> Blocks);
