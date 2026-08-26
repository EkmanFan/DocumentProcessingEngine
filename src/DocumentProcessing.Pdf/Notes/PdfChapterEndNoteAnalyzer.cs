using System.Globalization;
using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Pdf.Notes;

/// <summary>
/// Concludes chapter-end note relations by correlating a sequential numbered
/// payload section with raised inline references in the preceding source span.
/// </summary>
/// <remarks>
/// Entry boundaries are reconstructed from native word geometry and may occur
/// inside one source text block. Numeric line starts without correlated raised
/// references remain ordinary payload text and cannot create notes by
/// themselves.
/// </remarks>
internal sealed class PdfChapterEndNoteAnalyzer
    : IPdfDocumentNoteStrategy
{
    #region Variables and Constants

    private const int MinimumSequentialEntries =
        3;

    private const int MaximumSequentialEntryPageGap =
        1;

    private const double MinimumVisualLineTolerance =
        0.0010;

    private const double VisualLineHeightToleranceRatio =
        0.45;

    private const double MaximumEntryMarkerHorizontalDrift =
        0.01;

    #endregion

    #region Methods Analysis

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

        var references =
            extraction.Pages
                .OrderBy(
                    page =>
                        page.PhysicalPageNumber)
                .SelectMany(
                    page =>
                        PdfRaisedNumericReferenceFinder
                            .Find(
                                page.PhysicalPageNumber,
                                page.Blocks))
                .Where(
                    reference =>
                        !claimedReferences.Contains(
                            reference.Key))
                .ToArray();

        if (references.Length ==
            0)
        {
            return [];
        }

        var lines =
            BuildLines(
                extraction,
                cancellationToken);

        var candidateSections =
            FindCandidateSections(
                lines);

        if (candidateSections.Count ==
            0)
        {
            return [];
        }

        var notes =
            new List<PagedNativeDocumentNote>();

        PdfSourcePosition? previousSectionEnd =
            null;

        foreach (var section in
                 candidateSections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sectionStart =
                PdfSourcePosition.From(
                    section.Entries[0].Line);

            var availableReferences =
                references
                    .Where(
                        reference =>
                            PdfSourcePosition.From(
                                    reference)
                                .CompareTo(
                                    sectionStart) <
                                0 &&
                            (
                                previousSectionEnd is null ||
                                PdfSourcePosition.From(
                                        reference)
                                    .CompareTo(
                                        previousSectionEnd.Value) >
                                    0
                            ))
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

            var correlatedEntries =
                section.Entries
                    .Where(
                        entry =>
                            availableReferences.TryGetValue(
                                entry.Label,
                                out var matching) &&
                            matching.Length ==
                                1)
                    .ToArray();

            var sectionEnd =
                PdfSourcePosition.From(
                    lines[
                        section.EndLineIndex]);

            if (availableReferences.Count !=
                    section.Entries.Count ||
                correlatedEntries.Length !=
                    section.Entries.Count)
            {
                previousSectionEnd =
                    sectionEnd;

                continue;
            }

            var projectedEntries =
                correlatedEntries
                    .Select(
                        entry =>
                            new
                            {
                                Entry =
                                    entry,
                                MatchingReference =
                                    availableReferences[
                                        entry.Label][0],
                                PayloadLines =
                                    ProjectPayloadLines(
                                        section,
                                        entry,
                                        lines)
                            })
                    .ToArray();

            if (projectedEntries.Any(
                    projected =>
                        projected.PayloadLines.Count ==
                            0))
            {
                previousSectionEnd =
                    sectionEnd;

                continue;
            }

            foreach (var projected in
                     projectedEntries)
            {
                var entry =
                    projected.Entry;

                var matchingReference =
                    projected.MatchingReference;

                var payloadLines =
                    projected.PayloadLines;

                var sourceBlocks =
                    payloadLines
                        .SelectMany(
                            line =>
                                line.SourceBlockSequences
                                    .Select(
                                        sourceSequence =>
                                            new PagedNativeNoteSourceBlock(
                                                line.PhysicalPageNumber,
                                                sourceSequence)))
                        .Distinct()
                        .ToArray();

                notes.Add(
                    new PagedNativeDocumentNote(
                        entry.Label,
                        [
                            new PagedNativeNoteReference(
                                entry.Label,
                                matchingReference.PhysicalPageNumber,
                                matchingReference.SourceBlockSequence,
                                matchingReference.Word.SourceSequence,
                                matchingReference.Word.Bounds)
                        ],
                        payloadLines,
                        sourceBlocks));
            }

            previousSectionEnd =
                sectionEnd;
        }

        return notes;
    }

    private static IReadOnlyList<ChapterEndSection> FindCandidateSections(
        IReadOnlyList<ChapterEndLine> lines)
    {
        var sections =
            new List<ChapterEndSection>();

        List<ChapterEndEntryStart>? current =
            null;

        foreach (var line in
                 lines)
        {
            if (!TryReadEntryLabel(
                    line,
                    out var label,
                    out var numericLabel))
            {
                continue;
            }

            if (numericLabel ==
                1)
            {
                AddSectionIfConclusive(
                    current,
                    lines,
                    sections);

                current =
                    [
                        new ChapterEndEntryStart(
                            label,
                            numericLabel,
                            line)
                    ];

                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (!IsAlignedWithSection(
                    line,
                    current[0].Line))
            {
                continue;
            }

            if (line.PhysicalPageNumber -
                    current[^1].Line.PhysicalPageNumber >
                MaximumSequentialEntryPageGap)
            {
                AddSectionIfConclusive(
                    current,
                    lines,
                    sections);

                current =
                    null;

                continue;
            }

            if (numericLabel !=
                current[^1].NumericLabel +
                1)
            {
                AddSectionIfConclusive(
                    current,
                    lines,
                    sections);

                current =
                    null;

                continue;
            }

            current.Add(
                new ChapterEndEntryStart(
                    label,
                    numericLabel,
                    line));
        }

        AddSectionIfConclusive(
            current,
            lines,
            sections);

        return sections;
    }

    private static bool IsAlignedWithSection(
        ChapterEndLine candidate,
        ChapterEndLine sectionStart) =>
        Math.Abs(
            candidate.Words[0].Word.Bounds.Left -
            sectionStart.Words[0].Word.Bounds.Left) <=
        MaximumEntryMarkerHorizontalDrift;

    private static void AddSectionIfConclusive(
        IReadOnlyList<ChapterEndEntryStart>? entries,
        IReadOnlyList<ChapterEndLine> lines,
        ICollection<ChapterEndSection> sections)
    {
        if (entries is null ||
            entries.Count <
            MinimumSequentialEntries)
        {
            return;
        }

        var lastEntry =
            entries[^1];

        var endLineIndex =
            lines
                .Where(
                    line =>
                        line.GlobalIndex >=
                            lastEntry.Line.GlobalIndex &&
                        line.PhysicalPageNumber ==
                            lastEntry.Line.PhysicalPageNumber &&
                        line.SourceBlockSequence ==
                            lastEntry.Line.SourceBlockSequence)
                .Max(
                    line =>
                        line.GlobalIndex);

        sections.Add(
            new ChapterEndSection(
                entries.ToArray(),
                endLineIndex));
    }

    private static bool TryReadEntryLabel(
        ChapterEndLine line,
        out string label,
        out int numericLabel)
    {
        label =
            string.Empty;

        numericLabel =
            0;

        if (line.Words.Count ==
            0)
        {
            return false;
        }

        var marker =
            line.Words[0].Word.Text;

        if (marker.Length is
                < 2 or
                > 4 ||
            marker[^1] is not
                ('.' or ')'))
        {
            return false;
        }

        var digits =
            marker[..^1];

        if (!digits.All(
                char.IsAsciiDigit) ||
            digits.Length >
                1 &&
            digits[0] ==
                '0' ||
            !int.TryParse(
                digits,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out numericLabel) ||
            numericLabel <=
                0)
        {
            return false;
        }

        label =
            numericLabel.ToString(
                CultureInfo.InvariantCulture);

        return true;
    }

    private static IReadOnlyList<PagedNativeNotePayloadLine>
        ProjectPayloadLines(
        ChapterEndSection section,
        ChapterEndEntryStart entry,
        IReadOnlyList<ChapterEndLine> lines)
    {
        var entryIndex =
            section.Entries
                .Select(
                    (candidate, index) =>
                        new
                        {
                            Candidate =
                                candidate,
                            Index =
                                index
                        })
                .Single(
                    candidate =>
                        candidate.Candidate.Line.GlobalIndex ==
                        entry.Line.GlobalIndex)
                .Index;

        var endExclusive =
            entryIndex +
                1 <
            section.Entries.Count
                ? section.Entries[
                        entryIndex +
                        1]
                    .Line.GlobalIndex
                : section.EndLineIndex +
                  1;

        var sectionBlocks =
            section.Entries
                .Select(
                    item =>
                        new ChapterEndBlockKey(
                            item.Line.PhysicalPageNumber,
                            item.Line.SourceBlockSequence))
                .ToHashSet();

        var payload =
            new List<PagedNativeNotePayloadLine>();

        foreach (var line in
                 lines.Where(
                     candidate =>
                         candidate.GlobalIndex >=
                             entry.Line.GlobalIndex &&
                         candidate.GlobalIndex <
                             endExclusive &&
                         sectionBlocks.Contains(
                             new ChapterEndBlockKey(
                                 candidate.PhysicalPageNumber,
                                 candidate.SourceBlockSequence))))
        {
            var words =
                line.GlobalIndex ==
                    entry.Line.GlobalIndex
                    ? line.Words
                        .Skip(1)
                        .ToArray()
                    : line.Words;

            if (words.Count ==
                0)
            {
                continue;
            }

            payload.Add(
                ToPayloadLine(
                    line.PhysicalPageNumber,
                    words));
        }

        return payload;
    }

    #endregion

    #region Methods Visual Lines

    private static IReadOnlyList<ChapterEndLine> BuildLines(
        DocumentExtractionResult extraction,
        CancellationToken cancellationToken)
    {
        var lines =
            new List<ChapterEndLine>();

        foreach (var page in
                 extraction.Pages
                     .OrderBy(
                         page =>
                             page.PhysicalPageNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var block in
                     page.Blocks
                         .OrderBy(
                             block =>
                                 block.ReadingOrder ??
                                 int.MaxValue)
                         .ThenBy(
                             block =>
                                 block.SourceSequence))
            {
                foreach (var visualLine in
                         BuildVisualLines(
                             block))
                {
                    lines.Add(
                        new ChapterEndLine(
                            lines.Count,
                            page.PhysicalPageNumber,
                            block.SourceSequence,
                            visualLine.Bounds,
                            visualLine.Words));
                }
            }
        }

        return lines;
    }

    private static IReadOnlyList<VisualLine> BuildVisualLines(
        DocumentTextBlock block)
    {
        var words =
            block.Words
                .Select(
                    word =>
                        new LocatedWord(
                            block.SourceSequence,
                            word))
                .ToArray();

        if (words.Length ==
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

        var groups =
            new List<MutableVisualLine>();

        foreach (var word in
                 words
                     .OrderBy(
                         item =>
                             CenterY(
                                 item.Word))
                     .ThenBy(
                         item =>
                             item.Word.Bounds.Left)
                     .ThenBy(
                         item =>
                             item.Word.SourceSequence))
        {
            var centerY =
                CenterY(
                    word.Word);

            var best =
                groups
                    .Select(
                        (line, index) =>
                            new
                            {
                                Line =
                                    line,
                                Index =
                                    index,
                                Distance =
                                    Math.Abs(
                                        centerY -
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
                            candidate.Index)
                    .FirstOrDefault();

            if (best is null)
            {
                var created =
                    new MutableVisualLine(
                        centerY);

                created.Words.Add(
                    word);

                groups.Add(
                    created);

                continue;
            }

            best.Line.Words.Add(
                word);

            best.Line.CenterY =
                best.Line.Words.Average(
                    item =>
                        CenterY(
                            item.Word));
        }

        return groups
            .OrderBy(
                line =>
                    line.CenterY)
            .Select(
                line =>
                {
                    var ordered =
                        line.Words
                            .OrderBy(
                                word =>
                                    word.Word.Bounds.Left)
                            .ThenBy(
                                word =>
                                    word.Word.SourceSequence)
                            .ToArray();

                    return new VisualLine(
                        CombineBounds(
                            ordered.Select(
                                word =>
                                    word.Word.Bounds)),
                        ordered);
                })
            .ToArray();
    }

    private static PagedNativeNotePayloadLine ToPayloadLine(
        int physicalPageNumber,
        IReadOnlyList<LocatedWord> words) =>
        new(
            physicalPageNumber,
            string.Join(
                " ",
                words.Select(
                    word =>
                        word.Word.Text)),
            CombineBounds(
                words.Select(
                    word =>
                        word.Word.Bounds)),
            words
                .Select(
                    word =>
                        word.SourceBlockSequence)
                .Distinct()
                .ToArray(),
            words
                .Select(
                    word =>
                        word.Word.SourceSequence)
                .ToArray());

    #endregion

    #region Methods Math

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

    private readonly record struct ChapterEndBlockKey(
        int PhysicalPageNumber,
        int SourceBlockSequence);

    private readonly record struct PdfSourcePosition(
        int PhysicalPageNumber,
        double Top,
        double Left)
        : IComparable<PdfSourcePosition>
    {
        public int CompareTo(
            PdfSourcePosition other)
        {
            var pageComparison =
                PhysicalPageNumber.CompareTo(
                    other.PhysicalPageNumber);

            if (pageComparison !=
                0)
            {
                return pageComparison;
            }

            var topComparison =
                Top.CompareTo(
                    other.Top);

            return topComparison !=
                    0
                ? topComparison
                : Left.CompareTo(
                    other.Left);
        }

        public static PdfSourcePosition From(
            ChapterEndLine line) =>
            new(
                line.PhysicalPageNumber,
                line.Bounds.Top,
                line.Bounds.Left);

        public static PdfSourcePosition From(
            PdfRaisedNumericReferenceCandidate reference) =>
            new(
                reference.PhysicalPageNumber,
                reference.Word.Bounds.Top,
                reference.Word.Bounds.Left);
    }

    private sealed record ChapterEndLine(
        int GlobalIndex,
        int PhysicalPageNumber,
        int SourceBlockSequence,
        NormalizedRectangle Bounds,
        IReadOnlyList<LocatedWord> Words);

    private sealed record ChapterEndEntryStart(
        string Label,
        int NumericLabel,
        ChapterEndLine Line);

    private sealed record ChapterEndSection(
        IReadOnlyList<ChapterEndEntryStart> Entries,
        int EndLineIndex);

    private sealed record LocatedWord(
        int SourceBlockSequence,
        DocumentWord Word);

    private sealed record VisualLine(
        NormalizedRectangle Bounds,
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

    #endregion
}
