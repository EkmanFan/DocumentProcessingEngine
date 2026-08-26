using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Documents.Notes;
using DocumentProcessing.Pdf.Notes;

namespace DocumentProcessing.UnitTests.Pdf;

public sealed class PdfBottomOfPageNoteAnalyzerTests
{
    #region Tests

    [Fact]
    public void AnalyzeEvidence_ReconstructsSplitBlockMultilineAndSingleTokenNotes()
    {
        var page =
            new PagedNativeNotePageEvidence(
                1,
                [
                    BodyBlock(
                        sourceSequence:
                            0,
                        ("body", "10"),
                        ("text", "11")),
                    LabelBlock(
                        sourceSequence:
                            1,
                        (10, 0.70),
                        (11, 0.80)),
                    PayloadBlock(
                        sourceSequence:
                            2,
                        ("A long first line", 0.70),
                        ("continues here", 0.74),
                        ("γενητῶν.", 0.80)),
                    StandaloneNumberBlock(
                        sourceSequence:
                            3,
                        "156",
                        pointSize:
                            6,
                        centerY:
                            0.50)
                ]);

        var result =
            PdfBottomOfPageNoteAnalyzer
                .AnalyzeEvidence(
                    [
                        page
                    ]);

        Assert.Equal(
            2,
            result.Count);

        var ten =
            result.Single(
                entry =>
                    entry.Label ==
                    "10");

        Assert.Equal(
            "A long first line continues here",
            ten.Text);

        Assert.Equal(
            2,
            ten.PayloadLines.Count);

        Assert.Contains(
            new PagedNativeNoteSourceBlock(
                1,
                1),
            ten.SourceBlocks);

        Assert.Contains(
            new PagedNativeNoteSourceBlock(
                1,
                2),
            ten.SourceBlocks);

        var eleven =
            result.Single(
                entry =>
                    entry.Label ==
                    "11");

        Assert.Equal(
            "γενητῶν.",
            eleven.Text);

        Assert.DoesNotContain(
            result,
            entry =>
                entry.Label ==
                "156");
    }

    [Fact]
    public void AnalyzeEvidence_AppendsOnlyGenuineCrossPageContinuation()
    {
        var pageOne =
            new PagedNativeNotePageEvidence(
                1,
                [
                    BodyBlock(
                        sourceSequence:
                            0,
                        ("body", "20")),
                    LabelBlock(
                        sourceSequence:
                            1,
                        (20, 0.80)),
                    PayloadBlock(
                        sourceSequence:
                            2,
                        ("starts here", 0.80))
                ]);

        var continuationAndNext =
            new DocumentTextBlock(
                sourceSequence:
                    2,
                readingOrder:
                    2,
                "continues on next page\n21\nnext note",
                new NormalizedRectangle(
                    0.10,
                    0.55,
                    0.90,
                    0.86),
                words:
                    [
                        Word(
                            20,
                            "continues",
                            0.12,
                            0.60,
                            0.20,
                            0.62,
                            9),
                        Word(
                            21,
                            "on",
                            0.21,
                            0.60,
                            0.24,
                            0.62,
                            9),
                        Word(
                            22,
                            "next",
                            0.25,
                            0.60,
                            0.30,
                            0.62,
                            9),
                        Word(
                            23,
                            "page",
                            0.31,
                            0.60,
                            0.37,
                            0.62,
                            9),
                        Word(
                            24,
                            "21",
                            0.12,
                            0.80,
                            0.15,
                            0.82,
                            9),
                        Word(
                            25,
                            "next",
                            0.18,
                            0.80,
                            0.23,
                            0.82,
                            9),
                        Word(
                            26,
                            "note",
                            0.24,
                            0.80,
                            0.29,
                            0.82,
                            9)
                    ],
                medianPointSize:
                    9,
                lineCount:
                    3);

        var pageTwo =
            new PagedNativeNotePageEvidence(
                2,
                [
                    BodyBlock(
                        sourceSequence:
                            0,
                        ("body", "21")),
                    continuationAndNext
                ]);

        var result =
            PdfBottomOfPageNoteAnalyzer
                .AnalyzeEvidence(
                    [
                        pageOne,
                        pageTwo
                    ]);

        var twenty =
            result.Single(
                entry =>
                    entry.Label ==
                    "20");

        Assert.True(
            twenty.SpansMultiplePages);

        Assert.Equal(
            "starts here continues on next page",
            twenty.Text);

        Assert.Equal(
            new[]
            {
                1,
                2
            },
            twenty.PayloadLines
                .Select(
                    line =>
                        line.PhysicalPageNumber)
                .Distinct()
                .ToArray());

        var twentyOne =
            result.Single(
                entry =>
                    entry.Label ==
                    "21");

        Assert.Equal(
            "next note",
            twentyOne.Text);
    }

    [Fact]
    public void AnalyzeEvidence_DoesNotTreatFirstNextPageLabelLineAsContinuation()
    {
        var pageOne =
            new PagedNativeNotePageEvidence(
                1,
                [
                    BodyBlock(
                        sourceSequence:
                            0,
                        ("body", "30")),
                    LabelBlock(
                        sourceSequence:
                            1,
                        (30, 0.80)),
                    PayloadBlock(
                        sourceSequence:
                            2,
                        ("complete note", 0.80))
                ]);

        var pageTwo =
            new PagedNativeNotePageEvidence(
                2,
                [
                    BodyBlock(
                        sourceSequence:
                            0,
                        ("body", "31")),
                    LabelAndPayloadBlock(
                        sourceSequence:
                            2,
                        label:
                            "31",
                        payload:
                            "new note",
                        centerY:
                            0.80)
                ]);

        var result =
            PdfBottomOfPageNoteAnalyzer
                .AnalyzeEvidence(
                    [
                        pageOne,
                        pageTwo
                    ]);

        var thirty =
            result.Single(
                entry =>
                    entry.Label ==
                    "30");

        Assert.False(
            thirty.SpansMultiplePages);

        Assert.Equal(
            "complete note",
            thirty.Text);
    }

    [Fact]
    public void AnalyzeEvidence_AppendsUnlabeledBottomContinuationOnSparseNextPage()
    {
        var pageOne =
            new PagedNativeNotePageEvidence(
                1,
                [
                    BodyBlock(
                        sourceSequence:
                            0,
                        ("body", "50")),
                    LabelBlock(
                        sourceSequence:
                            1,
                        (50, 0.84)),
                    PayloadBlock(
                        sourceSequence:
                            2,
                        ("starts on first page", 0.84))
                ]);

        var pageTwo =
            new PagedNativeNotePageEvidence(
                2,
                [
                    TextBlock(
                        sourceSequence:
                            0,
                        "body continuation",
                        centerY:
                            0.20,
                        pointSize:
                            11),
                    PayloadBlock(
                        sourceSequence:
                            1,
                        ("continues without a repeated label", 0.84))
                ]);

        var result =
            Assert.Single(
                PdfBottomOfPageNoteAnalyzer
                    .AnalyzeEvidence(
                        [
                            pageOne,
                            pageTwo
                        ]));

        Assert.True(
            result.SpansMultiplePages);

        Assert.Equal(
            "starts on first page continues without a repeated label",
            result.Text);

        Assert.Equal(
            new[]
            {
                1,
                2
            },
            result.PayloadLines
                .Select(line =>
                    line.PhysicalPageNumber)
                .Distinct()
                .ToArray());
    }

    [Fact]
    public void AnalyzeEvidence_DoesNotAppendUnlabeledBlockWhenPreviousNoteEndsAboveFooter()
    {
        var pageOne =
            new PagedNativeNotePageEvidence(
                1,
                [
                    BodyBlock(
                        sourceSequence:
                            0,
                        ("body", "60")),
                    LabelBlock(
                        sourceSequence:
                            1,
                        (60, 0.70)),
                    PayloadBlock(
                        sourceSequence:
                            2,
                        ("complete note", 0.70))
                ]);

        var pageTwo =
            new PagedNativeNotePageEvidence(
                2,
                [
                    TextBlock(
                        sourceSequence:
                            0,
                        "body continuation",
                        centerY:
                            0.20,
                        pointSize:
                            11),
                    PayloadBlock(
                        sourceSequence:
                            1,
                        ("unrelated small footer text", 0.84))
                ]);

        var result =
            Assert.Single(
                PdfBottomOfPageNoteAnalyzer
                    .AnalyzeEvidence(
                        [
                            pageOne,
                            pageTwo
                        ]));

        Assert.False(
            result.SpansMultiplePages);

        Assert.Equal(
            "complete note",
            result.Text);
    }

    [Fact]
    public void AnalyzeEvidence_FailsClosedOnAmbiguousDuplicateLabel()
    {
        var page =
            new PagedNativeNotePageEvidence(
                1,
                [
                    BodyBlock(
                        sourceSequence:
                            0,
                        ("body", "40")),
                    LabelBlock(
                        sourceSequence:
                            1,
                        (40, 0.70)),
                    LabelBlock(
                        sourceSequence:
                            2,
                        (40, 0.80)),
                    PayloadBlock(
                        sourceSequence:
                            3,
                        ("ambiguous", 0.80))
                ]);

        var result =
            PdfBottomOfPageNoteAnalyzer
                .AnalyzeEvidence(
                    [
                        page
                    ]);

        Assert.Empty(
            result);

    }

    [Fact]
    public void AnalyzeEvidence_HonorsCancellation()
    {
        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () =>
                PdfBottomOfPageNoteAnalyzer
                    .AnalyzeEvidence(
                        [
                            new PagedNativeNotePageEvidence(
                                1,
                                [])
                        ],
                        cancellation.Token));
    }

    #endregion


    #region Helpers

    private static DocumentTextBlock BodyBlock(
        int sourceSequence,
        params (string Anchor, string Label)[] references)
    {
        var words =
            new List<DocumentWord>();

        var x =
            0.10;

        var wordSequence =
            sourceSequence *
            100;

        foreach (var reference in
                 references)
        {
            var anchorWidth =
                0.06;

            words.Add(
                Word(
                    wordSequence++,
                    reference.Anchor,
                    x,
                    0.30,
                    x +
                    anchorWidth,
                    0.32,
                    11));

            words.Add(
                Word(
                    wordSequence++,
                    reference.Label,
                    x +
                    anchorWidth +
                    0.001,
                    0.288,
                    x +
                    anchorWidth +
                    0.025,
                    0.306,
                    9));

            x +=
                0.18;
        }

        return new DocumentTextBlock(
            sourceSequence,
            readingOrder:
                sourceSequence,
            string.Join(
                " ",
                references.Select(
                    item =>
                        $"{item.Anchor} {item.Label}")),
            new NormalizedRectangle(
                0.08,
                0.25,
                0.92,
                0.35),
            words,
            medianPointSize:
                11,
            lineCount:
                1);
    }

    private static DocumentTextBlock LabelBlock(
        int sourceSequence,
        params (int Label, double CenterY)[] labels)
    {
        var words =
            labels
                .Select(
                    (label, index) =>
                        Word(
                            sourceSequence *
                                100 +
                            index,
                            label.Label.ToString(),
                            0.10,
                            label.CenterY -
                                0.01,
                            0.13,
                            label.CenterY +
                                0.01,
                            9))
                .ToArray();

        return new DocumentTextBlock(
            sourceSequence,
            readingOrder:
                sourceSequence,
            string.Join(
                "\n",
                labels.Select(
                    label =>
                        label.Label.ToString())),
            new NormalizedRectangle(
                0.09,
                labels.Min(
                    label =>
                        label.CenterY) -
                    0.02,
                0.14,
                labels.Max(
                    label =>
                        label.CenterY) +
                    0.02),
            words,
            medianPointSize:
                9,
            lineCount:
                labels.Length);
    }

    private static DocumentTextBlock PayloadBlock(
        int sourceSequence,
        params (string Text, double CenterY)[] lines)
    {
        var words =
            new List<DocumentWord>();

        var wordSequence =
            sourceSequence *
            100;

        foreach (var line in
                 lines)
        {
            var x =
                0.18;

            foreach (var value in
                     line.Text.Split(
                         ' ',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var width =
                    Math.Max(
                        0.03,
                        value.Length *
                        0.008);

                words.Add(
                    Word(
                        wordSequence++,
                        value,
                        x,
                        line.CenterY -
                            0.01,
                        x +
                        width,
                        line.CenterY +
                            0.01,
                        9));

                x +=
                    width +
                    0.01;
            }
        }

        return new DocumentTextBlock(
            sourceSequence,
            readingOrder:
                sourceSequence,
            string.Join(
                "\n",
                lines.Select(
                    line =>
                        line.Text)),
            new NormalizedRectangle(
                0.16,
                lines.Min(
                    line =>
                        line.CenterY) -
                    0.02,
                0.92,
                lines.Max(
                    line =>
                        line.CenterY) +
                    0.02),
            words,
            medianPointSize:
                9,
            lineCount:
                lines.Length);
    }

    private static DocumentTextBlock LabelAndPayloadBlock(
        int sourceSequence,
        string label,
        string payload,
        double centerY)
    {
        var words =
            new List<DocumentWord>
            {
                Word(
                    sourceSequence *
                        100,
                    label,
                    0.10,
                    centerY -
                        0.01,
                    0.13,
                    centerY +
                        0.01,
                    9)
            };

        var x =
            0.18;

        var sequence =
            sourceSequence *
                100 +
            1;

        foreach (var value in
                 payload.Split(
                     ' ',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var width =
                Math.Max(
                    0.03,
                    value.Length *
                    0.008);

            words.Add(
                Word(
                    sequence++,
                    value,
                    x,
                    centerY -
                        0.01,
                    x +
                    width,
                    centerY +
                        0.01,
                    9));

            x +=
                width +
                0.01;
        }

        return new DocumentTextBlock(
            sourceSequence,
            readingOrder:
                sourceSequence,
            $"{label}\n{payload}",
            new NormalizedRectangle(
                0.09,
                centerY -
                    0.02,
                0.92,
                centerY +
                    0.02),
            words,
            medianPointSize:
                9,
            lineCount:
                1);
    }

    private static DocumentTextBlock StandaloneNumberBlock(
        int sourceSequence,
        string value,
        double pointSize,
        double centerY) =>
        new(
            sourceSequence,
            readingOrder:
                sourceSequence,
            value,
            new NormalizedRectangle(
                0.90,
                centerY -
                    0.01,
                0.94,
                centerY +
                    0.01),
            [
                Word(
                    sourceSequence *
                        100,
                    value,
                    0.90,
                    centerY -
                        0.01,
                    0.94,
                    centerY +
                        0.01,
                    pointSize)
            ],
            medianPointSize:
                pointSize,
            lineCount:
                1);

    private static DocumentTextBlock TextBlock(
        int sourceSequence,
        string text,
        double centerY,
        double pointSize)
    {
        var words =
            text.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(
                    (value, index) =>
                        Word(
                            sourceSequence *
                                100 +
                            index,
                            value,
                            0.10 +
                                index *
                                0.10,
                            centerY -
                                0.01,
                            0.18 +
                                index *
                                0.10,
                            centerY +
                                0.01,
                            pointSize))
                .ToArray();

        return new DocumentTextBlock(
            sourceSequence,
            readingOrder:
                sourceSequence,
            text,
            new NormalizedRectangle(
                words.Min(word =>
                    word.Bounds.Left),
                words.Min(word =>
                    word.Bounds.Top),
                words.Max(word =>
                    word.Bounds.Right),
                words.Max(word =>
                    word.Bounds.Bottom)),
            words,
            medianPointSize:
                pointSize,
            lineCount:
                1);
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
