using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Reconciliation;

public sealed class ComparableNativeOcrReconciliationTests
{
    [Fact]
    public void ReconcileComparable_DehyphenatesEquivalentExtentAndRecordsAgreement()
    {
        var block =
            Block(
                "compan\u00AD ions",
                Word(0, "compan\u00AD", 0.20, 0.20, 0.30, 0.25),
                Word(1, "ions", 0.31, 0.20, 0.40, 0.25));

        var ocr =
            Ocr(
                block.Bounds,
                (0, "compan-"),
                (1, "ions"));

        var input =
            new TextReconciliationInput(
                physicalPageNumber: 233,
                NativeTextStatus.Healthy,
                block,
                ocr);

        var extent =
            Assert.IsType<ComparableNativeTextExtent>(
                NativeTextExtentProjector.Project(
                    block,
                    ocr.SourceLayoutObservation));

        var result =
            NativeOcrTextReconciler.ReconcileComparable(
                input,
                extent);

        Assert.Equal(
            TextReconciliationDecision.Agreement,
            result.Decision);

        Assert.Equal(
            TextSelectionOrigin.Native,
            result.SelectedOrigin);

        Assert.Equal(
            "companions",
            result.SelectedText);

        Assert.Equal(
            "companions",
            result.OcrText);

        Assert.True(
            result.TextsEquivalent);

        Assert.Same(
            extent,
            result.ComparableNativeExtent);

        Assert.Equal(
            1,
            result.NativeTextPreparation?.BoundaryJoinCount);

        Assert.Equal(
            1,
            result.OcrTextPreparation?.BoundaryJoinCount);
    }

    [Fact]
    public void ReconcileComparable_SuspiciousCharacterDifferenceRemainsConflict()
    {
        var block =
            Block(
                "conversion",
                Word(0, "conversion", 0.20, 0.20, 0.40, 0.25));

        var ocr =
            Ocr(
                block.Bounds,
                (0, "conversior"));

        var input =
            new TextReconciliationInput(
                physicalPageNumber: 233,
                NativeTextStatus.Suspicious,
                block,
                ocr);

        var extent =
            Assert.IsType<ComparableNativeTextExtent>(
                NativeTextExtentProjector.Project(
                    block,
                    ocr.SourceLayoutObservation));

        var result =
            NativeOcrTextReconciler.ReconcileComparable(
                input,
                extent);

        Assert.Equal(
            TextReconciliationDecision.Conflict,
            result.Decision);

        Assert.Equal(
            TextSelectionOrigin.None,
            result.SelectedOrigin);

        Assert.False(
            result.TextsEquivalent);

        Assert.True(
            result.HasDivergence);
    }

    [Fact]
    public void ReconcileComparable_HealthyCharacterDifferenceKeepsPreparedNativeExtent()
    {
        var block =
            Block(
                "historian",
                Word(0, "historian", 0.20, 0.20, 0.40, 0.25));

        var ocr =
            Ocr(
                block.Bounds,
                (0, "historlan"));

        var input =
            new TextReconciliationInput(
                physicalPageNumber: 233,
                NativeTextStatus.Healthy,
                block,
                ocr);

        var extent =
            Assert.IsType<ComparableNativeTextExtent>(
                NativeTextExtentProjector.Project(
                    block,
                    ocr.SourceLayoutObservation));

        var result =
            NativeOcrTextReconciler.ReconcileComparable(
                input,
                extent);

        Assert.Equal(
            TextReconciliationDecision.HealthyNativePreferred,
            result.Decision);

        Assert.Equal(
            TextSelectionOrigin.Native,
            result.SelectedOrigin);

        Assert.Equal(
            "historian",
            result.SelectedText);

        Assert.False(
            result.TextsEquivalent);
    }

    [Fact]
    public void ReconcileComparable_RejectsMissingNativeStatus()
    {
        var ocr =
            Ocr(
                new NormalizedRectangle(
                    0.20,
                    0.20,
                    0.60,
                    0.40),
                (0, "OCR"));

        var raw =
            new TextReconciliationInput(
                physicalPageNumber: 233,
                NativeTextStatus.Missing,
                nativeBlock: null,
                ocr);

        var block =
            Block(
                "native",
                Word(0, "native", 0.20, 0.20, 0.40, 0.25));

        var extent =
            new ComparableNativeTextExtent(
                block,
                ocr.SourceLayoutObservation,
                firstWordIndex: 0,
                lastWordIndex: 0,
                intersectingWordCount: 1,
                block.Words);

        Assert.Throws<ArgumentException>(
            () =>
                NativeOcrTextReconciler.ReconcileComparable(
                    raw,
                    extent));
    }

    [Fact]
    public void ReconcileComparable_RejectsExtentFromDifferentNativeBlock()
    {
        var block =
            Block(
                "one",
                Word(0, "one", 0.20, 0.20, 0.30, 0.25));

        var otherBlock =
            Block(
                "two",
                Word(0, "two", 0.20, 0.20, 0.30, 0.25));

        var ocr =
            Ocr(
                block.Bounds,
                (0, "one"));

        var input =
            new TextReconciliationInput(
                physicalPageNumber: 233,
                NativeTextStatus.Healthy,
                block,
                ocr);

        var extent =
            new ComparableNativeTextExtent(
                otherBlock,
                ocr.SourceLayoutObservation,
                firstWordIndex: 0,
                lastWordIndex: 0,
                intersectingWordCount: 1,
                otherBlock.Words);

        Assert.Throws<ArgumentException>(
            () =>
                NativeOcrTextReconciler.ReconcileComparable(
                    input,
                    extent));
    }

    [Fact]
    public void ReconcileComparable_RejectsExtentFromDifferentLayoutObservation()
    {
        var block =
            Block(
                "one",
                Word(0, "one", 0.20, 0.20, 0.30, 0.25));

        var ocr =
            Ocr(
                block.Bounds,
                (0, "one"));

        var input =
            new TextReconciliationInput(
                physicalPageNumber: 233,
                NativeTextStatus.Healthy,
                block,
                ocr);

        var otherLayout =
            new LayoutObservation(
                physicalPageNumber: 233,
                observationSequence: 4,
                readingOrder: 4,
                LayoutObservationKind.Text,
                block.Bounds,
                rawLabel: "text");

        var extent =
            new ComparableNativeTextExtent(
                block,
                otherLayout,
                firstWordIndex: 0,
                lastWordIndex: 0,
                intersectingWordCount: 1,
                block.Words);

        Assert.Throws<ArgumentException>(
            () =>
                NativeOcrTextReconciler.ReconcileComparable(
                    input,
                    extent));
    }

    [Fact]
    public void RawReconcile_RemainsBlockLevelForCompatibility()
    {
        var block =
            Block(
                "first paragraph second paragraph",
                Word(
                    0,
                    "first paragraph second paragraph",
                    0.20,
                    0.20,
                    0.60,
                    0.40));

        var ocr =
            Ocr(
                block.Bounds,
                (0, "first paragraph"));

        var result =
            NativeOcrTextReconciler.Reconcile(
                new TextReconciliationInput(
                    physicalPageNumber: 233,
                    NativeTextStatus.Healthy,
                    block,
                    ocr));

        Assert.Equal(
            TextReconciliationDecision.HealthyNativePreferred,
            result.Decision);

        Assert.Null(
            result.ComparableNativeExtent);

        Assert.Null(
            result.NativeTextPreparation);

        Assert.Null(
            result.OcrTextPreparation);
    }

    private static DocumentWord Word(
        int sourceSequence,
        string text,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            sourceSequence,
            text,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom));

    private static DocumentTextBlock Block(
        string text,
        params DocumentWord[] words) =>
        new(
            sourceSequence: 3,
            readingOrder: 3,
            text,
            new NormalizedRectangle(
                0.15,
                0.15,
                0.65,
                0.45),
            words);

    private static OcrRegionResult Ocr(
        NormalizedRectangle bounds,
        params (int Sequence, string Text)[] fragments)
    {
        var layout =
            new LayoutObservation(
                physicalPageNumber: 233,
                observationSequence: 3,
                readingOrder: 3,
                LayoutObservationKind.Text,
                bounds,
                rawLabel: "text");

        var observations =
            fragments
                .Select(
                    fragment =>
                        new OcrTextObservation(
                            physicalPageNumber: 233,
                            sourceLayoutObservationSequence: 3,
                            fragment.Sequence,
                            fragment.Text,
                            confidence: 0.95,
                            bounds))
                .ToArray();

        return new OcrRegionResult(
            "paddleocr-general-ocr",
            "test-profile-v1",
            layout,
            observations);
    }
}
