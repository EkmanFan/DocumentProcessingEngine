using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Pdf.Notes;

namespace DocumentProcessing.UnitTests.Pdf;

public sealed class PdfChapterEndNoteAnalyzerTests
{
    #region Tests

    [Fact]
    public void Analyze_CorrelatesSequentialEntriesAndSplitsOneNativeBlock()
    {
        var extraction =
            CreateExtraction(
                includeReferences:
                    true);

        var notes =
            new PdfChapterEndNoteAnalyzer()
                .Analyze(
                    extraction,
                    new HashSet<PdfNativeNoteReferenceKey>());

        Assert.Equal(
            3,
            notes.Count);

        Assert.Equal(
            new[]
            {
                "1",
                "2",
                "3"
            },
            notes.Select(
                note =>
                    note.Label));

        Assert.Equal(
            "first payload 1996) remains payload",
            notes[0].Text);

        Assert.Equal(
            "second payload",
            notes[1].Text);

        Assert.Equal(
            "third payload",
            notes[2].Text);

        Assert.All(
            notes,
            note =>
                Assert.Equal(
                    new[]
                    {
                        new PagedNativeNoteSourceBlock(
                            2,
                            0)
                    },
                    note.SourceBlocks));
    }

    [Fact]
    public void Analyze_DoesNotConcludeNumberedSectionWithoutReferences()
    {
        var notes =
            new PdfChapterEndNoteAnalyzer()
                .Analyze(
                    CreateExtraction(
                        includeReferences:
                            false),
                    new HashSet<PdfNativeNoteReferenceKey>());

        Assert.Empty(
            notes);
    }

    [Fact]
    public void Analyze_FailsClosedWhenSectionIsOnlyPartiallyCorrelated()
    {
        var notes =
            new PdfChapterEndNoteAnalyzer()
                .Analyze(
                    CreateExtraction(
                        includeReferences:
                            true,
                        includeUnreferencedFourthEntry:
                            true),
                    new HashSet<PdfNativeNoteReferenceKey>());

        Assert.Empty(
            notes);
    }

    [Fact]
    public void Analyze_FailsClosedWhenOneCorrelatedEntryHasNoPayload()
    {
        var notes =
            new PdfChapterEndNoteAnalyzer()
                .Analyze(
                    CreateExtraction(
                        includeReferences:
                            true,
                        includeEmptySecondEntry:
                            true),
                    new HashSet<PdfNativeNoteReferenceKey>());

        Assert.Empty(
            notes);
    }

    [Fact]
    public void Analyze_IgnoresMisalignedNumericPayloadLine()
    {
        var notes =
            new PdfChapterEndNoteAnalyzer()
                .Analyze(
                    CreateExtraction(
                        includeReferences:
                            true,
                        includeMisalignedNumericPayload:
                            true),
                    new HashSet<PdfNativeNoteReferenceKey>());

        Assert.Equal(
            3,
            notes.Count);

        Assert.Equal(
            "second payload 30. remains payload",
            notes[1].Text);
    }

    [Fact]
    public void Analyze_DoesNotBridgeSequentialEntriesAcrossDistantPages()
    {
        var notes =
            new PdfChapterEndNoteAnalyzer()
                .Analyze(
                    CreateExtraction(
                        includeReferences:
                            true,
                        includeDistantFourthEntry:
                            true),
                    new HashSet<PdfNativeNoteReferenceKey>());

        Assert.Equal(
            3,
            notes.Count);
    }

    [Fact]
    public void Analyze_DoesNotReuseClaimedReference()
    {
        var extraction =
            CreateExtraction(
                includeReferences:
                    true);

        var claimed =
            new HashSet<PdfNativeNoteReferenceKey>
            {
                new(
                    1,
                    0,
                    1)
            };

        var notes =
            new PdfChapterEndNoteAnalyzer()
                .Analyze(
                    extraction,
                    claimed);

        Assert.Empty(
            notes);
    }

    #endregion

    #region Helpers

    private static DocumentExtractionResult CreateExtraction(
        bool includeReferences,
        bool includeUnreferencedFourthEntry = false,
        bool includeEmptySecondEntry = false,
        bool includeMisalignedNumericPayload = false,
        bool includeDistantFourthEntry = false)
    {
        var pages =
            new List<DocumentExtractionPage>();

        if (includeReferences)
        {
            pages.Add(
                new DocumentExtractionPage(
                    1,
                    "body 1 body 2 body 3",
                    blocks:
                        [
                            BodyBlock()
                        ]));
        }

        var chapterEndText =
            includeUnreferencedFourthEntry
                ? "1. first payload\n1996) remains payload\n2. second payload\n3. third payload\n4. unreferenced payload"
                : includeEmptySecondEntry
                    ? "1. first payload\n1996) remains payload\n2.\n3. third payload"
                    : includeMisalignedNumericPayload
                        ? "1. first payload\n1996) remains payload\n2. second payload\n30. remains payload\n3. third payload"
                        : "1. first payload\n1996) remains payload\n2. second payload\n3. third payload";

        pages.Add(
            new DocumentExtractionPage(
                2,
                chapterEndText,
                blocks:
                    [
                        ChapterEndBlock(
                            chapterEndText,
                            includeUnreferencedFourthEntry,
                            includeEmptySecondEntry,
                            includeMisalignedNumericPayload)
                    ]));

        if (includeDistantFourthEntry)
        {
            pages.Add(
                new DocumentExtractionPage(
                    5,
                    "4. distant numbered payload",
                    blocks:
                        [
                            new DocumentTextBlock(
                                sourceSequence:
                                    0,
                                readingOrder:
                                    0,
                                "4. distant numbered payload",
                                new NormalizedRectangle(
                                    0.1,
                                    0.2,
                                    0.9,
                                    0.4),
                                words:
                                    [
                                        Word(0, "4.", 0.15, 0.30, 0.18, 0.32, 10),
                                        Word(1, "distant", 0.20, 0.30, 0.29, 0.32, 10),
                                        Word(2, "numbered", 0.30, 0.30, 0.41, 0.32, 10),
                                        Word(3, "payload", 0.42, 0.30, 0.52, 0.32, 10)
                                    ],
                                medianPointSize:
                                    10,
                                lineCount:
                                    1)
                        ]));
        }

        return new DocumentExtractionResult(
            DocumentFormatId.Pdf,
            pages);
    }

    private static DocumentTextBlock BodyBlock() =>
        new(
            sourceSequence:
                0,
            readingOrder:
                0,
            "body 1 body 2 body 3",
            new NormalizedRectangle(
                0.1,
                0.2,
                0.9,
                0.4),
            words:
                [
                    Word(0, "body", 0.10, 0.30, 0.18, 0.32, 12),
                    Word(1, "1", 0.181, 0.285, 0.195, 0.303, 9),
                    Word(2, "body", 0.30, 0.30, 0.38, 0.32, 12),
                    Word(3, "2", 0.381, 0.285, 0.395, 0.303, 9),
                    Word(4, "body", 0.50, 0.30, 0.58, 0.32, 12),
                    Word(5, "3", 0.581, 0.285, 0.595, 0.303, 9)
                ],
            medianPointSize:
                12,
            lineCount:
                1);

    private static DocumentTextBlock ChapterEndBlock(
        string text,
        bool includeUnreferencedFourthEntry,
        bool includeEmptySecondEntry,
        bool includeMisalignedNumericPayload)
    {
        var words =
            new List<DocumentWord>
            {
                Word(0, "1.", 0.15, 0.30, 0.18, 0.32, 10),
                Word(1, "first", 0.20, 0.30, 0.27, 0.32, 10),
                Word(2, "payload", 0.28, 0.30, 0.38, 0.32, 10),
                Word(3, "1996)", 0.10, 0.40, 0.17, 0.42, 10),
                Word(4, "remains", 0.18, 0.40, 0.27, 0.42, 10),
                Word(5, "payload", 0.28, 0.40, 0.38, 0.42, 10),
                Word(6, "2.", 0.15, 0.50, 0.18, 0.52, 10)
            };

        if (!includeEmptySecondEntry)
        {
            words.AddRange(
                [
                    Word(7, "second", 0.20, 0.50, 0.29, 0.52, 10),
                    Word(8, "payload", 0.30, 0.50, 0.40, 0.52, 10)
                ]);
        }

        if (includeMisalignedNumericPayload)
        {
            words.AddRange(
                [
                    Word(15, "30.", 0.10, 0.55, 0.14, 0.57, 10),
                    Word(16, "remains", 0.15, 0.55, 0.24, 0.57, 10),
                    Word(17, "payload", 0.25, 0.55, 0.35, 0.57, 10)
                ]);
        }

        words.AddRange(
            [
                Word(9, "3.", 0.15, 0.60, 0.18, 0.62, 10),
                Word(10, "third", 0.20, 0.60, 0.27, 0.62, 10),
                Word(11, "payload", 0.28, 0.60, 0.38, 0.62, 10)
            ]);

        if (includeUnreferencedFourthEntry)
        {
            words.AddRange(
                [
                    Word(12, "4.", 0.15, 0.70, 0.18, 0.72, 10),
                    Word(13, "unreferenced", 0.20, 0.70, 0.34, 0.72, 10),
                    Word(14, "payload", 0.35, 0.70, 0.45, 0.72, 10)
                ]);
        }

        return new DocumentTextBlock(
            sourceSequence:
                0,
            readingOrder:
                0,
            text,
            new NormalizedRectangle(
                0.1,
                0.2,
                0.9,
                0.8),
            words,
            medianPointSize:
                10,
            lineCount:
                4 +
                (
                    includeUnreferencedFourthEntry
                        ? 1
                        : 0
                ) +
                (
                    includeMisalignedNumericPayload
                        ? 1
                        : 0
                ));
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
