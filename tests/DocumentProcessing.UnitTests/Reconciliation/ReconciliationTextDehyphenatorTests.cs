using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Reconciliation;

public sealed class ReconciliationTextDehyphenatorTests
{
    [Fact]
    public void DehyphenateNative_JoinsTrailingSoftHyphenBeforeLowercaseWord()
    {
        var result =
            ReconciliationTextDehyphenator.DehyphenateNative(
                Extent(
                    "compan\u00AD",
                    "ions,"));

        Assert.Equal(
            "companions,",
            result.Text);

        Assert.Equal(
            1,
            result.SoftHyphenRemovalCount);

        Assert.Equal(
            1,
            result.BoundaryJoinCount);

        Assert.True(
            result.Changed);
    }

    [Fact]
    public void DehyphenateNative_RemovesInteriorSoftHyphenWithoutJoiningWords()
    {
        var result =
            ReconciliationTextDehyphenator.DehyphenateNative(
                Extent(
                    "of\u00ADfice",
                    "study"));

        Assert.Equal(
            "office study",
            result.Text);

        Assert.Equal(
            1,
            result.SoftHyphenRemovalCount);

        Assert.Equal(
            0,
            result.BoundaryJoinCount);
    }

    [Fact]
    public void DehyphenateNative_PreservesOrdinaryHardHyphenAcrossWordBoundary()
    {
        var result =
            ReconciliationTextDehyphenator.DehyphenateNative(
                Extent(
                    "well-",
                    "being"));

        Assert.Equal(
            "well- being",
            result.Text);

        Assert.False(
            result.Changed);
    }

    [Fact]
    public void DehyphenateNative_DoesNotJoinSoftHyphenBeforeUppercaseWord()
    {
        var result =
            ReconciliationTextDehyphenator.DehyphenateNative(
                Extent(
                    "Upper\u00AD",
                    "Case"));

        Assert.Equal(
            "Upper Case",
            result.Text);

        Assert.Equal(
            1,
            result.SoftHyphenRemovalCount);

        Assert.Equal(
            0,
            result.BoundaryJoinCount);
    }

    [Fact]
    public void DehyphenateOcr_JoinsAsciiHyphenOnlyAcrossObservationBoundary()
    {
        var result =
            ReconciliationTextDehyphenator.DehyphenateOcr(
                Ocr(
                    (0, "compan-"),
                    (1, "ions,")));

        Assert.Equal(
            "companions,",
            result.Text);

        Assert.Equal(
            0,
            result.SoftHyphenRemovalCount);

        Assert.Equal(
            1,
            result.BoundaryJoinCount);
    }

    [Fact]
    public void DehyphenateOcr_PreservesHardHyphenInsideOneObservation()
    {
        var result =
            ReconciliationTextDehyphenator.DehyphenateOcr(
                Ocr(
                    (0, "well-being")));

        Assert.Equal(
            "well-being",
            result.Text);

        Assert.False(
            result.Changed);
    }

    [Fact]
    public void DehyphenateOcr_DoesNotJoinBeforeUppercaseObservation()
    {
        var result =
            ReconciliationTextDehyphenator.DehyphenateOcr(
                Ocr(
                    (0, "Upper-"),
                    (1, "Case")));

        Assert.Equal(
            "Upper- Case",
            result.Text);

        Assert.False(
            result.Changed);
    }

    [Fact]
    public void DehyphenateOcr_UsesObservationSequenceNotInputCollectionOrder()
    {
        var result =
            ReconciliationTextDehyphenator.DehyphenateOcr(
                Ocr(
                    (2, "world"),
                    (0, "inter-"),
                    (1, "national")));

        Assert.Equal(
            "international world",
            result.Text);

        Assert.Equal(
            1,
            result.BoundaryJoinCount);
    }

    [Fact]
    public void Dehyphenation_DoesNotNormalizeCasePunctuationOrSpelling()
    {
        var native =
            ReconciliationTextDehyphenator.DehyphenateNative(
                Extent(
                    "Hist0rian,",
                    "Colour"));

        var ocr =
            ReconciliationTextDehyphenator.DehyphenateOcr(
                Ocr(
                    (0, "historian"),
                    (1, "color")));

        Assert.Equal(
            "Hist0rian, Colour",
            native.Text);

        Assert.Equal(
            "historian color",
            ocr.Text);

        Assert.False(
            NativeOcrTextReconciler.AreConservativelyEquivalent(
                native.Text,
                ocr.Text));
    }

    [Fact]
    public void DehyphenateOcr_EmptyRegionProducesEmptyUnchangedResult()
    {
        var result =
            ReconciliationTextDehyphenator.DehyphenateOcr(
                Ocr());

        Assert.Equal(
            string.Empty,
            result.Text);

        Assert.False(
            result.Changed);
    }

    private static ComparableNativeTextExtent Extent(
        params string[] wordTexts)
    {
        var words =
            wordTexts
                .Select(
                    (text, index) =>
                        new DocumentWord(
                            index,
                            text,
                            new NormalizedRectangle(
                                0.10 + index * 0.05,
                                0.20,
                                0.14 + index * 0.05,
                                0.24)))
                .ToArray();

        var block =
            new DocumentTextBlock(
                sourceSequence: 3,
                readingOrder: 3,
                text: string.Join(
                    " ",
                    wordTexts),
                new NormalizedRectangle(
                    0.10,
                    0.20,
                    0.80,
                    0.30),
                words);

        var layout =
            new LayoutObservation(
                physicalPageNumber: 1,
                observationSequence: 3,
                readingOrder: 3,
                LayoutObservationKind.Text,
                block.Bounds,
                rawLabel: "text");

        return new ComparableNativeTextExtent(
            block,
            layout,
            firstWordIndex: 0,
            lastWordIndex: words.Length - 1,
            intersectingWordCount: words.Length,
            words);
    }

    private static OcrRegionResult Ocr(
        params (int Sequence, string Text)[] fragments)
    {
        var layout =
            new LayoutObservation(
                physicalPageNumber: 1,
                observationSequence: 3,
                readingOrder: 3,
                LayoutObservationKind.Text,
                new NormalizedRectangle(
                    0.10,
                    0.20,
                    0.80,
                    0.30),
                rawLabel: "text");

        var observations =
            fragments
                .Select(
                    fragment =>
                        new OcrTextObservation(
                            physicalPageNumber: 1,
                            sourceLayoutObservationSequence: 3,
                            fragment.Sequence,
                            fragment.Text,
                            confidence: 0.95,
                            layout.Bounds))
                .ToArray();

        return new OcrRegionResult(
            "paddleocr-general-ocr",
            "test-profile-v1",
            layout,
            observations);
    }
}
