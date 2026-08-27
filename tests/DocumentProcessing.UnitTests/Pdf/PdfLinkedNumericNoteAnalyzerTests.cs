using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Pdf.Notes;

namespace DocumentProcessing.UnitTests.Pdf;

public sealed class PdfLinkedNumericNoteAnalyzerTests
{
    #region Tests

    [Fact]
    public void Analyze_ReconstructsSplitBlocksAndStopsBeforeStarredNote()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    SourcePage(),
                    new DocumentExtractionPage(
                        2,
                        "notes",
                        blocks:
                            [
                                MarkerBlock(
                                    (0, "1.", 0.60),
                                    (3, "2.", 0.72),
                                    (6, "3.", 0.84)),
                                PayloadBlock(
                                    (1, "first", 0.60, 0.16),
                                    (2, "payload", 0.60, 0.23),
                                    (10, "continues", 0.64, 0.16),
                                    (11, "here", 0.64, 0.26),
                                    (4, "second", 0.72, 0.16),
                                    (5, "payload", 0.72, 0.24),
                                    (7, "third", 0.84, 0.16),
                                    (8, "payload", 0.84, 0.23),
                                    (9, "*special", 0.90, 0.10),
                                    (12, "material", 0.90, 0.20)),
                                BodyBlock()
                            ])
                ]);

        var notes =
            new PdfLinkedNumericNoteAnalyzer()
                .Analyze(
                    extraction,
                    Observations(
                        entryPages:
                            [
                                2,
                                2,
                                2
                            ]),
                    new HashSet<PdfNativeNoteReferenceKey>());

        Assert.Equal(
            3,
            notes.Count);

        Assert.Equal(
            "first payload continues here",
            notes[0].Text);

        Assert.Equal(
            "second payload",
            notes[1].Text);

        Assert.Equal(
            "third payload",
            notes[2].Text);
    }

    [Fact]
    public void Analyze_StopsBeforeNearbyUnlinkedNumericEntry()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    SourcePage(),
                    new DocumentExtractionPage(
                        2,
                        "notes",
                        blocks:
                            [
                                MarkerBlock(
                                    (0, "1.", 0.60),
                                    (3, "2.", 0.72),
                                    (6, "3.", 0.84),
                                    (9, "5.", 0.90)),
                                PayloadBlock(
                                    (1, "first", 0.60, 0.16),
                                    (2, "payload", 0.60, 0.23),
                                    (4, "second", 0.72, 0.16),
                                    (5, "payload", 0.72, 0.24),
                                    (7, "third", 0.84, 0.16),
                                    (8, "payload", 0.84, 0.23),
                                    (10, "unlinked", 0.90, 0.16),
                                    (11, "material", 0.90, 0.25))
                            ])
                ]);

        var notes =
            new PdfLinkedNumericNoteAnalyzer()
                .Analyze(
                    extraction,
                    Observations(
                        entryPages:
                            [
                                2,
                                2,
                                2
                            ]),
                    new HashSet<PdfNativeNoteReferenceKey>());

        Assert.Equal(
            3,
            notes.Count);

        Assert.Equal(
            "third payload",
            notes[2].Text);
    }

    [Fact]
    public void Analyze_StopsBeforeConclusiveUnlinkedSectionReset()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    SourcePage(),
                    new DocumentExtractionPage(
                        2,
                        "notes",
                        blocks:
                            [
                                MarkerBlock(
                                    (0, "1.", 0.54),
                                    (3, "2.", 0.62),
                                    (6, "3.", 0.70),
                                    (9, "1.", 0.82),
                                    (12, "2.", 0.88),
                                    (15, "3.", 0.94)),
                                PayloadBlock(
                                    (1, "first", 0.54, 0.16),
                                    (2, "payload", 0.54, 0.23),
                                    (4, "second", 0.62, 0.16),
                                    (5, "payload", 0.62, 0.24),
                                    (7, "third", 0.70, 0.16),
                                    (8, "payload", 0.70, 0.23),
                                    (10, "next", 0.82, 0.16),
                                    (11, "section", 0.82, 0.23),
                                    (13, "next", 0.88, 0.16),
                                    (14, "section", 0.88, 0.23),
                                    (16, "next", 0.94, 0.16),
                                    (17, "section", 0.94, 0.23))
                            ])
                ]);

        var notes =
            new PdfLinkedNumericNoteAnalyzer()
                .Analyze(
                    extraction,
                    Observations(
                        entryPages:
                            [
                                2,
                                2,
                                2
                            ]),
                    new HashSet<PdfNativeNoteReferenceKey>());

        Assert.Equal(
            3,
            notes.Count);

        Assert.Equal(
            "third payload",
            notes[2].Text);
    }

    [Fact]
    public void Analyze_AppendsNoteSizedCrossPageContinuation()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    SourcePage(),
                    new DocumentExtractionPage(
                        2,
                        "first note",
                        blocks:
                            [
                                MarkerBlock(
                                    (0, "1.", 0.80)),
                                PayloadBlock(
                                    (1, "starts", 0.80, 0.16),
                                    (2, "here", 0.80, 0.23))
                            ]),
                    new DocumentExtractionPage(
                        3,
                        "continued notes",
                        blocks:
                            [
                                MarkerBlock(
                                    (3, "2.", 0.75),
                                    (6, "3.", 0.85)),
                                PayloadBlock(
                                    (9, "continues", 0.62, 0.16),
                                    (10, "there", 0.62, 0.26),
                                    (4, "second", 0.75, 0.16),
                                    (5, "payload", 0.75, 0.24),
                                    (7, "third", 0.85, 0.16),
                                    (8, "payload", 0.85, 0.23)),
                                BodyBlock()
                            ])
                ]);

        var notes =
            new PdfLinkedNumericNoteAnalyzer()
                .Analyze(
                    extraction,
                    Observations(
                        entryPages:
                            [
                                2,
                                3,
                                3
                            ]),
                    new HashSet<PdfNativeNoteReferenceKey>());

        Assert.Equal(
            3,
            notes.Count);

        Assert.True(
            notes[0].SpansMultiplePages);

        Assert.Equal(
            "starts here continues there",
            notes[0].Text);
    }

    [Fact]
    public void Analyze_FailsClosedWhenSectionIsPartiallyCorrelated()
    {
        var observations =
            Observations(
                    entryPages:
                        [
                            2,
                            2,
                            2
                        ])
                .Where(
                    observation =>
                        observation.HasEntryPunctuation ||
                        observation.Label !=
                        "3")
                .ToArray();

        var notes =
            new PdfLinkedNumericNoteAnalyzer()
                .Analyze(
                    SinglePageExtraction(),
                    observations,
                    new HashSet<PdfNativeNoteReferenceKey>());

        Assert.Empty(
            notes);
    }

    [Fact]
    public void Analyze_DoesNotReuseClaimedReference()
    {
        var notes =
            new PdfLinkedNumericNoteAnalyzer()
                .Analyze(
                    SinglePageExtraction(),
                    Observations(
                        entryPages:
                            [
                                2,
                                2,
                                2
                            ]),
                    new HashSet<PdfNativeNoteReferenceKey>
                    {
                        new(
                            1,
                            0,
                            3)
                    });

        Assert.Empty(
            notes);
    }

    #endregion

    #region Helpers

    private static DocumentExtractionResult SinglePageExtraction() =>
        new(
            DocumentFormatId.Pdf,
            [
                SourcePage(),
                new DocumentExtractionPage(
                    2,
                    "notes",
                    blocks:
                        [
                            MarkerBlock(
                                (0, "1.", 0.60),
                                (3, "2.", 0.72),
                                (6, "3.", 0.84)),
                            PayloadBlock(
                                (1, "first", 0.60, 0.16),
                                (2, "payload", 0.60, 0.23),
                                (4, "second", 0.72, 0.16),
                                (5, "payload", 0.72, 0.24),
                                (7, "third", 0.84, 0.16),
                                (8, "payload", 0.84, 0.23))
                        ])
            ]);

    private static DocumentExtractionPage SourcePage() =>
        new(
            1,
            "body 1 body 2 body 3",
            blocks:
                [
                    new DocumentTextBlock(
                        sourceSequence:
                            0,
                        readingOrder:
                            0,
                        "body 1 body 2 body 3",
                        new NormalizedRectangle(
                            0.10,
                            0.20,
                            0.90,
                            0.40),
                        words:
                            [
                                Word(0, "body", 0.10, 0.30, 0.17, 0.32, 12),
                                Word(1, "1", 0.18, 0.29, 0.19, 0.31, 8),
                                Word(2, "body", 0.30, 0.30, 0.37, 0.32, 12),
                                Word(3, "2", 0.38, 0.29, 0.39, 0.31, 8),
                                Word(4, "body", 0.50, 0.30, 0.57, 0.32, 12),
                                Word(5, "3", 0.58, 0.29, 0.59, 0.31, 8)
                            ],
                        medianPointSize:
                            12,
                        lineCount:
                            1)
                ]);

    private static DocumentTextBlock MarkerBlock(
        params (int Sequence, string Text, double Top)[] values) =>
        new(
            sourceSequence:
                0,
            readingOrder:
                0,
            string.Join(
                "\n",
                values.Select(
                    value =>
                        value.Text)),
            new NormalizedRectangle(
                0.10,
                values.Min(
                    value =>
                        value.Top),
                0.14,
                values.Max(
                    value =>
                        value.Top) +
                0.02),
            values.Select(
                    value =>
                        Word(
                            value.Sequence,
                            value.Text,
                            0.10,
                            value.Top,
                            0.13,
                            value.Top +
                            0.02,
                            8))
                .ToArray(),
            medianPointSize:
                8,
            lineCount:
                values.Length);

    private static DocumentTextBlock PayloadBlock(
        params (int Sequence, string Text, double Top, double Left)[] values) =>
        new(
            sourceSequence:
                1,
            readingOrder:
                1,
            "payload",
            new NormalizedRectangle(
                values.Min(
                    value =>
                        value.Left),
                values.Min(
                    value =>
                        value.Top),
                0.90,
                values.Max(
                    value =>
                        value.Top) +
                0.02),
            values.Select(
                    value =>
                        Word(
                            value.Sequence,
                            value.Text,
                            value.Left,
                            value.Top,
                            value.Left +
                            0.06,
                            value.Top +
                            0.02,
                            8))
                .ToArray(),
            medianPointSize:
                8,
            lineCount:
                values.Select(
                        value =>
                            value.Top)
                    .Distinct()
                    .Count());

    private static DocumentTextBlock BodyBlock() =>
        new(
            sourceSequence:
                2,
            readingOrder:
                2,
            "ordinary body",
            new NormalizedRectangle(
                0.10,
                0.20,
                0.90,
                0.30),
            words:
                [
                    Word(20, "ordinary", 0.10, 0.20, 0.20, 0.23, 12),
                    Word(21, "body", 0.21, 0.20, 0.28, 0.23, 12)
                ],
            medianPointSize:
                12,
            lineCount:
                1);

    private static IReadOnlyList<PdfNativeNumericLinkObservation>
        Observations(
        IReadOnlyList<int> entryPages)
    {
        var observations =
            new List<PdfNativeNumericLinkObservation>();

        for (var index =
                 0;
             index <
             3;
             index++)
        {
            var label =
                (index +
                 1).ToString();

            observations.Add(
                new PdfNativeNumericLinkObservation(
                    label,
                    1,
                    entryPages[index],
                    0,
                    index *
                    2 +
                    1,
                    new NormalizedRectangle(
                        0.18 +
                        index *
                        0.20,
                        0.29,
                        0.19 +
                        index *
                        0.20,
                        0.31),
                    HasEntryPunctuation:
                        false));
        }

        for (var index =
                 0;
             index <
             3;
             index++)
        {
            observations.Add(
                new PdfNativeNumericLinkObservation(
                    (index +
                     1).ToString(),
                    entryPages[index],
                    1,
                    0,
                    index *
                    3,
                    new NormalizedRectangle(
                        0.10,
                        0.60 +
                        index *
                        0.12,
                        0.13,
                        0.62 +
                        index *
                        0.12),
                    HasEntryPunctuation:
                        true));
        }

        return observations;
    }

    private static DocumentWord Word(
        int sourceSequence,
        string text,
        double left,
        double top,
        double right,
        double bottom,
        double pointSize) =>
        new(
            sourceSequence,
            text,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            fontName:
                "Test",
            medianPointSize:
                pointSize);

    #endregion
}
