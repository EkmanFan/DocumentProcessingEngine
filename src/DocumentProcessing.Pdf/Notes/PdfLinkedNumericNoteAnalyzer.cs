using System.Globalization;
using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Pdf.Notes;

/// <summary>
/// Concludes neutral numeric note relations from internal PDF links whose
/// payload markers link back to uniquely matching source calls.
/// </summary>
internal sealed class PdfLinkedNumericNoteAnalyzer
{
    #region Variables and Constants

    private const int MinimumSequentialEntries =
        3;

    private const int MaximumSequentialEntryPageGap =
        4;

    private const double MinimumNotePointSizeRatio =
        0.90;

    private const double MaximumNotePointSizeRatio =
        1.10;

    private const double MinimumVisualLineTolerance =
        0.0010;

    private const double VisualLineHeightToleranceRatio =
        0.45;

    private const double MaximumEntryMarkerHorizontalDrift =
        0.05;

    #endregion

    #region Methods

    public IReadOnlyList<PagedNativeDocumentNote> Analyze(
        DocumentExtractionResult extraction,
        IReadOnlyList<PdfNativeNumericLinkObservation> observations,
        IReadOnlySet<PdfNativeNoteReferenceKey> claimedReferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            observations);

        ArgumentNullException.ThrowIfNull(
            claimedReferences);

        cancellationToken.ThrowIfCancellationRequested();

        if (observations.Count ==
            0)
        {
            return [];
        }

        var sections =
            FindCandidateSections(
                observations);

        var sourceReferenceKeys =
            observations
                .Where(
                    IsSourceCandidate)
                .Select(
                    observation =>
                        new PdfNativeNoteReferenceKey(
                            observation.PhysicalPageNumber,
                            observation.SourceBlockSequence,
                            observation.WordSourceSequence))
                .ToHashSet();

        var notes =
            new List<PagedNativeDocumentNote>();

        foreach (var section in
                 sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var matchingSources =
                section.Entries
                    .Select(
                        entry =>
                            new
                            {
                                Entry =
                                    entry,
                                Sources =
                                    FindMatchingSources(
                                        entry,
                                        section,
                                        observations,
                                        claimedReferences)
                            })
                    .ToArray();

            if (matchingSources.Any(
                    matching =>
                        matching.Sources.Count !=
                        1))
            {
                continue;
            }

            var lines =
                BuildSectionLines(
                    extraction,
                    section,
                    sourceReferenceKeys,
                    cancellationToken);

            if (lines.Count ==
                0)
            {
                continue;
            }

            var entryLines =
                matchingSources
                    .Select(
                        matching =>
                            new LinkedEntryLine(
                                matching.Entry,
                                matching.Sources[0],
                                FindUniqueEntryLine(
                                    matching.Entry,
                                    lines)))
                    .ToArray();

            if (entryLines.Any(
                    entry =>
                        entry.Line is null) ||
                !AreStrictlyOrdered(
                    entryLines))
            {
                continue;
            }

            var resolvedEntries =
                entryLines
                    .Select(
                        entry =>
                            entry with
                            {
                                Line =
                                    entry.Line!
                            })
                    .ToArray();

            var projected =
                resolvedEntries
                    .Select(
                        (entry, index) =>
                            new
                            {
                                Entry =
                                    entry,
                                Payload =
                                    ProjectPayloadLines(
                                        entry,
                                        index +
                                                1 <
                                            resolvedEntries.Length
                                            ? resolvedEntries[
                                                index +
                                                1].Line
                                            : null,
                                        lines)
                            })
                    .ToArray();

            if (projected.Any(
                    entry =>
                        entry.Payload.Count ==
                        0))
            {
                continue;
            }

            foreach (var entry in
                     projected)
            {
                var sourceBlocks =
                    entry.Payload
                        .SelectMany(
                            line =>
                                line.SourceBlockSequences.Select(
                                    sourceSequence =>
                                        new PagedNativeNoteSourceBlock(
                                            line.PhysicalPageNumber,
                                            sourceSequence)))
                        .Distinct()
                        .ToArray();

                notes.Add(
                    new PagedNativeDocumentNote(
                        entry.Entry.Entry.Label,
                        [
                            new PagedNativeNoteReference(
                                entry.Entry.Entry.Label,
                                entry.Entry.Source.PhysicalPageNumber,
                                entry.Entry.Source.SourceBlockSequence,
                                entry.Entry.Source.WordSourceSequence,
                                entry.Entry.Source.MarkerBounds)
                        ],
                        entry.Payload,
                        sourceBlocks));
            }
        }

        return notes;
    }

    private static IReadOnlyList<LinkedSection> FindCandidateSections(
        IReadOnlyList<PdfNativeNumericLinkObservation> observations)
    {
        var sections =
            new List<LinkedSection>();

        List<PdfNativeNumericLinkObservation>? current =
            null;

        foreach (var candidate in
                 observations.Where(
                     IsPayloadCandidate))
        {
            var numericLabel =
                int.Parse(
                    candidate.Label,
                    CultureInfo.InvariantCulture);

            if (current is null)
            {
                current =
                    [candidate];

                continue;
            }

            var previousNumericLabel =
                int.Parse(
                    current[^1].Label,
                    CultureInfo.InvariantCulture);

            if (candidate.PhysicalPageNumber -
                    current[^1].PhysicalPageNumber >
                    MaximumSequentialEntryPageGap ||
                numericLabel ==
                    1)
            {
                AddSectionIfConclusive(
                    current,
                    sections);

                current =
                    [candidate];

                continue;
            }

            if (numericLabel ==
                previousNumericLabel +
                1)
            {
                current.Add(
                    candidate);
            }
        }

        AddSectionIfConclusive(
            current,
            sections);

        return sections;
    }

    private static void AddSectionIfConclusive(
        IReadOnlyList<PdfNativeNumericLinkObservation>? entries,
        ICollection<LinkedSection> sections)
    {
        if (entries is null ||
            entries.Count <
            MinimumSequentialEntries)
        {
            return;
        }

        sections.Add(
            new LinkedSection(
                entries.ToArray()));
    }

    private static IReadOnlyList<PdfNativeNumericLinkObservation>
        FindMatchingSources(
        PdfNativeNumericLinkObservation entry,
        LinkedSection section,
        IReadOnlyList<PdfNativeNumericLinkObservation> observations,
        IReadOnlySet<PdfNativeNoteReferenceKey> claimedReferences) =>
        observations
            .Where(
                source =>
                    IsSourceCandidate(
                        source) &&
                    string.Equals(
                        source.Label,
                        entry.Label,
                        StringComparison.Ordinal) &&
                    source.PhysicalPageNumber ==
                        entry.TargetPhysicalPageNumber &&
                    source.TargetPhysicalPageNumber >=
                        section.Entries[0].PhysicalPageNumber &&
                    source.TargetPhysicalPageNumber <=
                        section.Entries[^1].PhysicalPageNumber &&
                    !claimedReferences.Contains(
                        new PdfNativeNoteReferenceKey(
                            source.PhysicalPageNumber,
                            source.SourceBlockSequence,
                            source.WordSourceSequence)))
            .ToArray();

    private static IReadOnlyList<LinkedVisualLine> BuildSectionLines(
        DocumentExtractionResult extraction,
        LinkedSection section,
        IReadOnlySet<PdfNativeNoteReferenceKey> sourceReferenceKeys,
        CancellationToken cancellationToken)
    {
        var entryWords =
            section.Entries
                .Select(
                    entry =>
                        FindUniqueWord(
                            extraction,
                            entry))
                .ToArray();

        if (entryWords.Any(
                word =>
                    word?.MedianPointSize is not
                    (>
                        0)))
        {
            return [];
        }

        var notePointSize =
            Median(
                entryWords
                    .Select(
                        word =>
                            word!.MedianPointSize!.Value)
                    .OrderBy(
                        value =>
                            value)
                    .ToArray());

        var lines =
            new List<LinkedVisualLine>();

        foreach (var page in
                 extraction.Pages
                     .Where(
                         page =>
                             page.PhysicalPageNumber >=
                                 section.Entries[0].PhysicalPageNumber &&
                             page.PhysicalPageNumber <=
                                 section.Entries[^1].PhysicalPageNumber)
                     .OrderBy(
                         page =>
                             page.PhysicalPageNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var words =
                page.Blocks
                    .SelectMany(
                        block =>
                            block.Words
                                .Where(
                                    word =>
                                        IsNoteSized(
                                            word,
                                            notePointSize))
                                .Where(
                                    word =>
                                        !sourceReferenceKeys.Contains(
                                            new PdfNativeNoteReferenceKey(
                                                page.PhysicalPageNumber,
                                                block.SourceSequence,
                                                word.SourceSequence)))
                                .Select(
                                    word =>
                                        new LocatedWord(
                                            block.SourceSequence,
                                            word)))
                    .ToArray();

            foreach (var visualLine in
                     BuildVisualLines(
                         words))
            {
                lines.Add(
                    new LinkedVisualLine(
                        lines.Count,
                        page.PhysicalPageNumber,
                        visualLine.Bounds,
                        visualLine.Words));
            }
        }

        return lines;
    }

    private static DocumentWord? FindUniqueWord(
        DocumentExtractionResult extraction,
        PdfNativeNumericLinkObservation observation)
    {
        var matching =
            extraction.Pages
                .Where(
                    page =>
                        page.PhysicalPageNumber ==
                        observation.PhysicalPageNumber)
                .SelectMany(
                    page =>
                        page.Blocks)
                .Where(
                    block =>
                        block.SourceSequence ==
                        observation.SourceBlockSequence)
                .SelectMany(
                    block =>
                        block.Words)
                .Where(
                    word =>
                        word.SourceSequence ==
                        observation.WordSourceSequence)
                .ToArray();

        return matching.Length ==
                1
            ? matching[0]
            : null;
    }

    private static LinkedVisualLine? FindUniqueEntryLine(
        PdfNativeNumericLinkObservation entry,
        IReadOnlyList<LinkedVisualLine> lines)
    {
        var matching =
            lines
                .Where(
                    line =>
                        line.PhysicalPageNumber ==
                            entry.PhysicalPageNumber &&
                        line.Words.Any(
                            word =>
                                word.SourceBlockSequence ==
                                    entry.SourceBlockSequence &&
                                word.Word.SourceSequence ==
                                    entry.WordSourceSequence))
                .ToArray();

        return matching.Length ==
                1
            ? matching[0]
            : null;
    }

    private static bool AreStrictlyOrdered(
        IReadOnlyList<LinkedEntryLine> entries)
    {
        for (var index =
                 0;
             index <
             entries.Count;
             index++)
        {
            var line =
                entries[index].Line;

            if (line is null ||
                FindMarkerIndex(
                    entries[index].Entry,
                    line) !=
                0 ||
                index >
                    0 &&
                line.GlobalIndex <=
                    entries[
                        index -
                        1].Line!.GlobalIndex)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<PagedNativeNotePayloadLine>
        ProjectPayloadLines(
        LinkedEntryLine entry,
        LinkedVisualLine? nextEntryLine,
        IReadOnlyList<LinkedVisualLine> lines)
    {
        var line =
            entry.Line!;

        var nextNumericBoundary =
            lines
                .Where(
                    candidate =>
                        candidate.GlobalIndex >
                            line.GlobalIndex &&
                        IsPotentialEntryBoundary(
                            entry.Entry,
                            candidate,
                            line,
                            lines))
                .Select(
                    candidate =>
                        (int?)candidate.GlobalIndex)
                .FirstOrDefault();

        var endExclusive =
            new[]
                {
                    nextEntryLine?.GlobalIndex,
                    nextNumericBoundary
                }
                .Where(
                    value =>
                        value is not null)
                .Select(
                    value =>
                        value!.Value)
                .DefaultIfEmpty(
                    lines.Count)
                .Min();

        var payload =
            new List<PagedNativeNotePayloadLine>();

        foreach (var candidate in
                 lines.Where(
                     candidate =>
                         candidate.GlobalIndex >=
                             line.GlobalIndex &&
                         candidate.GlobalIndex <
                             endExclusive))
        {
            var projected =
                candidate.GlobalIndex ==
                    line.GlobalIndex
                    ? ProjectEntryStartLine(
                        entry.Entry,
                        candidate)
                    : ToPayloadLine(
                        candidate.PhysicalPageNumber,
                        candidate.Words,
                        candidate.Words.Select(
                            word =>
                                word.Word.Text));

            if (projected is not null)
            {
                payload.Add(
                    projected);
            }
        }

        return payload;
    }

    private static bool IsPotentialEntryBoundary(
        PdfNativeNumericLinkObservation entry,
        LinkedVisualLine candidate,
        LinkedVisualLine sectionEntry,
        IReadOnlyList<LinkedVisualLine> lines)
    {
        if (candidate.Words.Count ==
                0 ||
            Math.Abs(
                candidate.Words[0].Word.Bounds.Left -
                sectionEntry.Words[0].Word.Bounds.Left) >
            MaximumEntryMarkerHorizontalDrift)
        {
            return false;
        }

        var marker =
            candidate.Words[0].Word.Text;

        if (marker.StartsWith(
                "*",
                StringComparison.Ordinal))
        {
            return true;
        }

        if (!TryReadNumericEntryMarker(
                marker,
                out var numericLabel))
        {
            return false;
        }

        if (numericLabel ==
            1)
        {
            return HasSequentialResetEvidence(
                candidate,
                sectionEntry,
                lines);
        }

        var currentLabel =
            int.Parse(
                entry.Label,
                CultureInfo.InvariantCulture);

        return numericLabel >
                   currentLabel &&
               numericLabel <=
                   currentLabel +
                   10;
    }

    private static bool HasSequentialResetEvidence(
        LinkedVisualLine reset,
        LinkedVisualLine sectionEntry,
        IReadOnlyList<LinkedVisualLine> lines)
    {
        var expectedLabel =
            2;

        foreach (var candidate in
                 lines.Where(
                     candidate =>
                         candidate.GlobalIndex >
                             reset.GlobalIndex &&
                         Math.Abs(
                             candidate.Words[0].Word.Bounds.Left -
                             sectionEntry.Words[0].Word.Bounds.Left) <=
                         MaximumEntryMarkerHorizontalDrift))
        {
            if (!TryReadNumericEntryMarker(
                    candidate.Words[0].Word.Text,
                    out var numericLabel) ||
                numericLabel !=
                expectedLabel)
            {
                continue;
            }

            expectedLabel++;

            if (expectedLabel ==
                4)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadNumericEntryMarker(
        string marker,
        out int numericLabel)
    {
        numericLabel =
            0;

        if (marker.Length is
                < 2 or
                > 5 ||
            marker[^1] is not
                ('.' or ')'))
        {
            return false;
        }

        var digits =
            marker[..^1];

        return digits.All(
                   char.IsAsciiDigit) &&
               (
                   digits.Length ==
                       1 ||
                   digits[0] !=
                       '0'
               ) &&
               int.TryParse(
                   digits,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out numericLabel) &&
               numericLabel >
                   0;
    }

    private static PagedNativeNotePayloadLine? ProjectEntryStartLine(
        PdfNativeNumericLinkObservation entry,
        LinkedVisualLine line)
    {
        var markerIndex =
            FindMarkerIndex(
                entry,
                line);

        if (markerIndex !=
            0)
        {
            return null;
        }

        var markerWord =
            line.Words[0];

        if (!TryRemoveMarker(
                markerWord.Word.Text,
                entry.Label,
                out var markerRemainder))
        {
            return null;
        }

        var words =
            line.Words
                .Skip(
                    string.IsNullOrWhiteSpace(
                        markerRemainder)
                        ? 1
                        : 0)
                .ToArray();

        if (words.Length ==
            0)
        {
            return null;
        }

        var text =
            words.Select(
                    word =>
                        word.Word.Text)
                .ToArray();

        if (!string.IsNullOrWhiteSpace(
                markerRemainder))
        {
            text[0] =
                markerRemainder;
        }

        return ToPayloadLine(
            line.PhysicalPageNumber,
            words,
            text);
    }

    private static PagedNativeNotePayloadLine ToPayloadLine(
        int physicalPageNumber,
        IReadOnlyList<LocatedWord> words,
        IEnumerable<string> text)
    {
        var bounds =
            words.Select(
                    word =>
                        word.Word.Bounds)
                .ToArray();

        return new PagedNativeNotePayloadLine(
            physicalPageNumber,
            string.Join(
                " ",
                text),
            new NormalizedRectangle(
                bounds.Min(
                    value =>
                        value.Left),
                bounds.Min(
                    value =>
                        value.Top),
                bounds.Max(
                    value =>
                        value.Right),
                bounds.Max(
                    value =>
                        value.Bottom)),
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
    }

    private static int FindMarkerIndex(
        PdfNativeNumericLinkObservation entry,
        LinkedVisualLine line) =>
        line.Words
            .Select(
                (word, index) =>
                    new
                    {
                        Word =
                            word,
                        Index =
                            index
                    })
            .Single(
                candidate =>
                    candidate.Word.SourceBlockSequence ==
                        entry.SourceBlockSequence &&
                    candidate.Word.Word.SourceSequence ==
                        entry.WordSourceSequence)
            .Index;

    private static bool TryRemoveMarker(
        string text,
        string label,
        out string remainder)
    {
        remainder =
            string.Empty;

        foreach (var marker in
                 new[]
                 {
                     label +
                     ".",
                     label +
                     ")"
                 })
        {
            if (!text.StartsWith(
                    marker,
                    StringComparison.Ordinal))
            {
                continue;
            }

            remainder =
                text[marker.Length..];

            return true;
        }

        if (!string.Equals(
                text,
                label,
                StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool IsPayloadCandidate(
        PdfNativeNumericLinkObservation observation) =>
        observation.TargetPhysicalPageNumber <
            observation.PhysicalPageNumber ||
        observation.TargetPhysicalPageNumber ==
            observation.PhysicalPageNumber &&
        observation.HasEntryPunctuation;

    private static bool IsSourceCandidate(
        PdfNativeNumericLinkObservation observation) =>
        observation.TargetPhysicalPageNumber >
            observation.PhysicalPageNumber ||
        observation.TargetPhysicalPageNumber ==
            observation.PhysicalPageNumber &&
        !observation.HasEntryPunctuation;

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
                heights);

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
                                    word.SourceBlockSequence)
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

    private static bool IsNoteSized(
        DocumentWord word,
        double notePointSize)
    {
        if (word.MedianPointSize is not
                (>
                    0) ||
            !double.IsFinite(
                word.MedianPointSize.Value))
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

    private static NormalizedRectangle CombineBounds(
        IEnumerable<NormalizedRectangle> values)
    {
        var bounds =
            values.ToArray();

        return new NormalizedRectangle(
            bounds.Min(
                value =>
                    value.Left),
            bounds.Min(
                value =>
                    value.Top),
            bounds.Max(
                value =>
                    value.Right),
            bounds.Max(
                value =>
                    value.Bottom));
    }

    private static double CenterY(
        DocumentWord word) =>
        (
            word.Bounds.Top +
            word.Bounds.Bottom
        ) /
        2.0;

    private static double Median(
        IReadOnlyList<double> values) =>
        values.Count %
                   2 ==
               0
            ? (
                values[
                    values.Count /
                    2 -
                    1] +
                values[
                    values.Count /
                    2]
            ) /
              2.0
            : values[
                values.Count /
                2];

    #endregion

    #region Internal Types

    private sealed record LinkedSection(
        IReadOnlyList<PdfNativeNumericLinkObservation> Entries);

    private sealed record LinkedEntryLine(
        PdfNativeNumericLinkObservation Entry,
        PdfNativeNumericLinkObservation Source,
        LinkedVisualLine? Line);

    private sealed record LinkedVisualLine(
        int GlobalIndex,
        int PhysicalPageNumber,
        NormalizedRectangle Bounds,
        IReadOnlyList<LocatedWord> Words);

    private sealed record VisualLine(
        NormalizedRectangle Bounds,
        IReadOnlyList<LocatedWord> Words);

    private sealed record LocatedWord(
        int SourceBlockSequence,
        DocumentWord Word);

    private sealed class MutableVisualLine(
        double centerY)
    {
        public double CenterY
        {
            get;
            set;
        } = centerY;

        public List<LocatedWord> Words
        {
            get;
        } = [];
    }

    #endregion
}
