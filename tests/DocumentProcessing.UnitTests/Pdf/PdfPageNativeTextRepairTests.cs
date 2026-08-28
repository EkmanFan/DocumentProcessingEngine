using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Pdf;

namespace DocumentProcessing.UnitTests.Pdf;

public sealed class PdfPageNativeTextRepairTests
{
    #region Tests

    [Fact]
    public void Reconstruct_AttachesGeometricallyEvidencedDropCapToParagraph()
    {
        var body =
            Block(
                0,
                0,
                lineCount:
                    3,
                Word(1, "ven", 0.155, 0.100, 0.190, 0.120, 11),
                Word(2, "if", 0.195, 0.100, 0.210, 0.120, 11),
                Word(3, "all", 0.215, 0.100, 0.240, 0.120, 11));
        var dropCap =
            Block(
                1,
                1,
                lineCount:
                    1,
                Word(0, "E", 0.100, 0.104, 0.148, 0.164, 30));

        var result =
            Assert.Single(
                PdfPageNativeTextRepair.Reconstruct(
                    [body, dropCap]));

        Assert.Equal(
            "Even if all",
            result.Text);
        Assert.Equal(
            new[]
            {
                0,
                1,
                2,
                3
            },
            result.Words.Select(word =>
                word.SourceSequence));
        Assert.Equal(
            0,
            result.SourceSequence);
        Assert.Equal(
            0,
            result.ReadingOrder);
        Assert.Equal(
            3,
            result.LineCount);
        Assert.Equal(
            11,
            result.MedianPointSize);
    }

    [Fact]
    public void Reconstruct_RepairsNumericSuperscriptBeforeAttachingDropCap()
    {
        var body =
            Block(
                0,
                4,
                lineCount:
                    2,
                Word(1, "ccording", 0.100, 0.100, 0.170, 0.120, 11),
                Word(2, "thing", 0.180, 0.100, 0.230, 0.120, 11),
                Word(6, "their", 0.100, 0.140, 0.150, 0.160, 11));
        var numericFragment =
            Block(
                1,
                3,
                Word(3, "749", 0.230, 0.095, 0.250, 0.105, 9.13),
                Word(4, "’;", 0.260, 0.100, 0.280, 0.120, 11),
                Word(5, "and", 0.290, 0.100, 0.330, 0.120, 11));
        var dropCap =
            Block(
                2,
                0,
                lineCount:
                    1,
                Word(0, "A", 0.050, 0.104, 0.095, 0.164, 30));

        var result =
            Assert.Single(
                PdfPageNativeTextRepair.Reconstruct(
                    [dropCap, numericFragment, body]));

        Assert.Equal(
            "According thing 749 ’; and\ntheir",
            result.Text);
        Assert.Equal(
            new[]
            {
                0,
                1,
                2,
                3,
                4,
                5,
                6
            },
            result.Words.Select(word =>
                word.SourceSequence));
        Assert.Equal(
            3,
            result.ReadingOrder);
    }

    [Theory]
    [InlineData("Ordinary", 3)]
    [InlineData("ordinary", 1)]
    public void Reconstruct_DoesNotAttachSingleLetterWithoutCompleteDropCapEvidence(
        string firstBodyWord,
        int lineCount)
    {
        var body =
            Block(
                0,
                0,
                lineCount,
                Word(1, firstBodyWord, 0.155, 0.100, 0.220, 0.120, 11),
                Word(2, "body", 0.225, 0.100, 0.260, 0.120, 11),
                Word(3, "text", 0.265, 0.100, 0.300, 0.120, 11));
        var isolatedLetter =
            Block(
                1,
                1,
                lineCount:
                    1,
                Word(0, "A", 0.100, 0.104, 0.148, 0.164, 30));

        var result =
            PdfPageNativeTextRepair.Reconstruct(
                [body, isolatedLetter]);

        Assert.Equal(
            2,
            result.Count);
        Assert.Same(
            body,
            result[0]);
        Assert.Same(
            isolatedLetter,
            result[1]);
    }

    [Fact]
    public void Reconstruct_DoesNotAttachDropCapWhenGeometryMatchesTwoBodies()
    {
        var first =
            Block(
                0,
                0,
                lineCount:
                    2,
                Word(1, "lpha", 0.155, 0.100, 0.200, 0.120, 11),
                Word(2, "body", 0.205, 0.100, 0.240, 0.120, 11),
                Word(3, "text", 0.245, 0.100, 0.280, 0.120, 11));
        var second =
            Block(
                1,
                1,
                lineCount:
                    2,
                Word(4, "nother", 0.155, 0.101, 0.210, 0.121, 11),
                Word(5, "body", 0.215, 0.101, 0.250, 0.121, 11),
                Word(6, "text", 0.255, 0.101, 0.290, 0.121, 11));
        var isolatedLetter =
            Block(
                2,
                1,
                lineCount:
                    1,
                Word(0, "A", 0.100, 0.104, 0.148, 0.164, 30));

        var result =
            PdfPageNativeTextRepair.Reconstruct(
                [first, second, isolatedLetter]);

        Assert.Equal(
            3,
            result.Count);
    }

    [Fact]
    public void Reconstruct_RepairsCrossBlock749Morphology()
    {
        var main =
            Block(
                0,
                1,
                Word(0, "according", 0.10, 0.10, 0.17, 0.12, 11),
                Word(1, "thing", 0.18, 0.10, 0.23, 0.12, 11),
                Word(6, "their", 0.10, 0.14, 0.15, 0.16, 11));

        var fragment =
            Block(
                1,
                0,
                Word(2, "749", 0.23, 0.095, 0.25, 0.105, 9.13),
                Word(3, "’;", 0.26, 0.10, 0.28, 0.12, 11),
                Word(4, "and", 0.29, 0.10, 0.33, 0.12, 11),
                Word(5, "all", 0.34, 0.10, 0.37, 0.12, 11));

        var result =
            Assert.Single(
                PdfPageNativeTextRepair
                    .Reconstruct(
                        new[]
                        {
                            fragment,
                            main
                        }));

        Assert.Equal(
            "according thing 749 ’; and all\ntheir",
            result.Text);

        Assert.Equal(
            new[]
            {
                0,
                1,
                2,
                3,
                4,
                5,
                6
            },
            result.Words.Select(word =>
                word.SourceSequence));
    }

    [Fact]
    public void Reconstruct_RepairsSameBlock754Morphology()
    {
        var block =
            Block(
                0,
                0,
                Word(0, "complained", 0.10, 0.10, 0.19, 0.12, 11),
                Word(5, "754", 0.45, 0.095, 0.47, 0.105, 9.13),
                Word(6, "?’", 0.48, 0.10, 0.50, 0.12, 11),
                Word(7, "Insensate,", 0.51, 0.10, 0.59, 0.12, 11),
                Word(1, "still,", 0.20, 0.10, 0.25, 0.12, 11),
                Word(2, "as", 0.26, 0.10, 0.28, 0.12, 11),
                Word(3, "ignorant", 0.29, 0.10, 0.36, 0.12, 11),
                Word(4, "God", 0.40, 0.10, 0.45, 0.12, 11),
                Word(8, "verily", 0.10, 0.14, 0.15, 0.16, 11));

        var result =
            Assert.Single(
                PdfPageNativeTextRepair
                    .Reconstruct(
                        new[]
                        {
                            block
                        }));

        Assert.Equal(
            "complained still, as ignorant God 754 ?’ Insensate,\nverily",
            result.Text);
    }

    [Fact]
    public void Reconstruct_RepairsIsolated783Morphology()
    {
        var body =
            Block(
                0,
                0,
                Word(0, "who", 0.10, 0.10, 0.14, 0.12, 11),
                Word(1, "it", 0.15, 0.10, 0.17, 0.12, 11),
                Word(2, "was", 0.18, 0.10, 0.22, 0.12, 11),
                Word(4, "suggested", 0.25, 0.10, 0.33, 0.12, 11),
                Word(5, "later", 0.10, 0.14, 0.14, 0.16, 11));

        var marker =
            Block(
                1,
                1,
                Word(3, "783", 0.22, 0.095, 0.24, 0.105, 9.13));

        var result =
            Assert.Single(
                PdfPageNativeTextRepair
                    .Reconstruct(
                        new[]
                        {
                            body,
                            marker
                        }));

        Assert.Equal(
            "who it was 783 suggested\nlater",
            result.Text);
    }

    [Fact]
    public void Reconstruct_UsesMarkerBlockTypographyForSparseAnchorFragment()
    {
        var body =
            Block(
                0,
                0,
                Word(0, "things", 0.10, 0.10, 0.16, 0.12, 11),
                Word(1, "which", 0.17, 0.10, 0.22, 0.12, 11),
                Word(2, "do", 0.23, 0.10, 0.25, 0.12, 11),
                Word(4, "877", 0.33, 0.091, 0.35, 0.101, 9.13),
                Word(5, ".\u2019", 0.36, 0.10, 0.38, 0.12, 11),
                Word(6, "But", 0.39, 0.10, 0.43, 0.12, 11));

        var anchor =
            Block(
                1,
                1,
                Word(3, "appear", 0.25, 0.10, 0.33, 0.11, 11));

        var result =
            Assert.Single(
                PdfPageNativeTextRepair
                    .Reconstruct(
                        new[]
                        {
                            body,
                            anchor
                        }));

        Assert.Equal(
            "things which do appear 877 .\u2019 But",
            result.Text);

        Assert.Equal(
            new[]
            {
                0,
                1,
                2,
                3,
                4,
                5,
                6
            },
            result.Words.Select(word =>
                word.SourceSequence));
    }

    [Fact]
    public void Reconstruct_Repairs804And805WithoutDiscardingUnrelatedMarginalNumber()
    {
        var body =
            Block(
                0,
                0,
                Word(0, "partake", 0.10, 0.10, 0.17, 0.12, 11),
                Word(2, "804", 0.24, 0.095, 0.26, 0.105, 9.13),
                Word(3, ",", 0.27, 0.10, 0.28, 0.12, 11),
                Word(4, "who", 0.29, 0.10, 0.33, 0.12, 11),
                Word(5, "children", 0.10, 0.14, 0.18, 0.16, 11),
                Word(8, "For", 0.24, 0.14, 0.27, 0.16, 11));

        var father =
            Block(
                1,
                2,
                Word(1, "Father", 0.17, 0.10, 0.24, 0.12, 11));

        var marker805 =
            Block(
                2,
                3,
                Word(6, "805", 0.18, 0.135, 0.20, 0.145, 9.13),
                Word(7, ".’", 0.21, 0.14, 0.23, 0.16, 11));

        var marginalNumber =
            Block(
                3,
                1,
                Word(9, "156", 0.90, 0.30, 0.92, 0.31, 6));

        var result =
            PdfPageNativeTextRepair
                .Reconstruct(
                    new[]
                    {
                        body,
                        marginalNumber,
                        father,
                        marker805
                    });

        Assert.Equal(
            2,
            result.Count);

        Assert.Equal(
            "partake Father 804 , who\nchildren 805 .’ For",
            result[0].Text);

        Assert.Same(
            marginalNumber,
            result[1]);

        Assert.Equal(
            "156",
            result[1].Text);
    }

    [Fact]
    public void Reconstruct_UsesMedianBodyPointSizeForLineTolerance()
    {
        var block =
            Block(
                0,
                0,
                Word(0, "alpha", 0.10, 0.10, 0.14, 0.12, 11),
                Word(2, "900", 0.20, 0.095, 0.22, 0.105, 9.13),
                Word(3, "later", 0.10, 0.114, 0.14, 0.134, 11),
                Word(4, "text", 0.15, 0.114, 0.19, 0.134, 11),
                Word(1, "anchor", 0.15, 0.10, 0.20, 0.12, 11),
                Word(5, "Heading", 0.10, 0.28, 0.20, 0.32, 24));

        var result =
            Assert.Single(
                PdfPageNativeTextRepair
                    .Reconstruct(
                        new[]
                        {
                            block
                        }));

        Assert.Equal(
            "alpha anchor 900\nlater text\nHeading",
            result.Text);

        Assert.Equal(
            new[]
            {
                0,
                1,
                2,
                3,
                4,
                5
            },
            result.Words.Select(word =>
                word.SourceSequence));
    }

    [Fact]
    public void Reconstruct_AttachesIsolatedTrailingPunctuationAfterSameBlockMarker()
    {
        var body =
            Block(
                0,
                0,
                Word(0, "they", 0.10, 0.10, 0.14, 0.12, 11),
                Word(1, "say", 0.15, 0.10, 0.20, 0.12, 11),
                Word(2, "1001", 0.20, 0.095, 0.23, 0.105, 9.13),
                Word(4, "not", 0.24, 0.10, 0.27, 0.12, 11),
                Word(5, "I", 0.28, 0.10, 0.29, 0.12, 11));

        var punctuation =
            Block(
                1,
                1,
                Word(3, ",", 0.23, 0.10, 0.235, 0.12, 11));

        var result =
            Assert.Single(
                PdfPageNativeTextRepair
                    .Reconstruct(
                        new[]
                        {
                            body,
                            punctuation
                        }));

        Assert.Equal(
            "they say 1001 , not I",
            result.Text);

        Assert.Equal(
            new[]
            {
                0,
                1,
                2,
                3,
                4,
                5
            },
            result.Words.Select(word =>
                word.SourceSequence));
    }

    [Fact]
    public void Reconstruct_DoesNotAttachNonConsecutivePunctuationFragment()
    {
        var body =
            Block(
                0,
                0,
                Word(0, "they", 0.10, 0.10, 0.14, 0.12, 11),
                Word(1, "say", 0.15, 0.10, 0.20, 0.12, 11),
                Word(2, "1001", 0.20, 0.095, 0.23, 0.105, 9.13),
                Word(3, "not", 0.24, 0.10, 0.27, 0.12, 11));

        var punctuation =
            Block(
                1,
                1,
                Word(4, ",", 0.23, 0.10, 0.235, 0.12, 11));

        var result =
            PdfPageNativeTextRepair
                .Reconstruct(
                    new[]
                    {
                        body,
                        punctuation
                    });

        Assert.Equal(
            2,
            result.Count);

        Assert.Same(
            body,
            result[0]);

        Assert.Same(
            punctuation,
            result[1]);
    }

    [Fact]
    public void Reconstruct_FailsClosedForAmbiguousAnchor()
    {
        var first =
            Block(
                0,
                0,
                Word(0, "alpha", 0.10, 0.10, 0.20, 0.12, 11),
                Word(2, "900", 0.20, 0.095, 0.22, 0.105, 9.13));

        var second =
            Block(
                1,
                1,
                Word(1, "beta", 0.10, 0.10, 0.20, 0.12, 11));

        var result =
            PdfPageNativeTextRepair
                .Reconstruct(
                    new[]
                    {
                        first,
                        second
                    });

        Assert.Equal(
            2,
            result.Count);

        Assert.Same(
            first,
            result[0]);

        Assert.Same(
            second,
            result[1]);
    }

    #endregion


    #region Helpers

    private static DocumentWord Word(
        int sequence,
        string text,
        double left,
        double top,
        double right,
        double bottom,
        double pointSize) =>
        new(
            sequence,
            text,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            "Body",
            pointSize);

    private static DocumentTextBlock Block(
        int sourceSequence,
        int readingOrder,
        params DocumentWord[] words) =>
        Block(
            sourceSequence,
            readingOrder,
            lineCount:
                1,
            words);

    private static DocumentTextBlock Block(
        int sourceSequence,
        int readingOrder,
        int lineCount,
        params DocumentWord[] words) =>
        new(
            sourceSequence,
            readingOrder,
            string.Join(
                " ",
                words.Select(word =>
                    word.Text)),
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
            "Body",
            words.Max(word =>
                word.MedianPointSize ?? 11),
            lineCount:
                lineCount);

    #endregion
}
