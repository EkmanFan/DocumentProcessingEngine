using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Reconciliation;

public sealed class AggregateNativeOcrReconciliationTests
{
    [Fact]
    public void ReconcileComparable_MultiBlockAgreement_SelectsAggregateNativeText()
    {
        var layout =
            Layout(
                36,
                8,
                8,
                LayoutObservationKind.Heading,
                0.08,
                0.80,
                0.40,
                0.84);

        var firstBlock =
            Block(
                7,
                7,
                Word(100, "eox", 0.09, 0.81, 0.14, 0.82),
                Word(101, "1", 0.145, 0.81, 0.16, 0.82),
                Word(102, ".1", 0.165, 0.81, 0.19, 0.82));

        var secondBlock =
            Block(
                8,
                8,
                Word(103, "The", 0.20, 0.81, 0.24, 0.82),
                Word(104, "Canon", 0.245, 0.81, 0.29, 0.82),
                Word(105, "of", 0.295, 0.81, 0.315, 0.82),
                Word(106, "Scripture", 0.32, 0.81, 0.385, 0.82));

        var pairing =
            Assert.Single(
                NativeLayoutTextPairer.Pair(
                    new[]
                    {
                        firstBlock,
                        secondBlock
                    },
                    new[]
                    {
                        layout
                    }));

        var nativeEvidence =
            Assert.IsType<ComparableNativeTextEvidence>(
                pairing.ComparableNativeEvidence);

        var ocr =
            Ocr(
                layout,
                "eox 1 .1 The Canon of Scripture");

        var input =
            new TextReconciliationInput(
                36,
                NativeTextStatus.Unverified,
                nativeEvidence,
                ocr);

        var result =
            NativeOcrTextReconciler
                .ReconcileComparable(
                    input,
                    nativeEvidence);

        Assert.Equal(
            TextReconciliationDecision.Agreement,
            result.Decision);

        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            result.SelectedOrigin);

        Assert.Equal(
            "eox 1 .1 The Canon of Scripture",
            result.SelectedText);

        Assert.True(
            result.TextsEquivalent);

        Assert.Same(
            nativeEvidence,
            result.ComparableNativeEvidence);

        Assert.Null(
            result.ComparableNativeExtent);

        Assert.Equal(
            new[]
            {
                7,
                8
            },
            result.NativeSourceBlocks
                .Select(
                    block =>
                        block.SourceSequence)
                .ToArray());
    }

    [Fact]
    public void ReconcileComparable_SingleExtentAggregatePreservesLegacyExtent()
    {
        var layout =
            Layout(
                405,
                9,
                9,
                LayoutObservationKind.Text,
                0.57,
                0.46,
                0.90,
                0.93);

        var block =
            Block(
                6,
                6,
                Word(0, "After", 0.58, 0.47, 0.63, 0.49),
                Word(1, "leaving", 0.64, 0.47, 0.70, 0.49),
                Word(2, "Thessalonica,", 0.71, 0.47, 0.82, 0.49));

        var pairing =
            Assert.Single(
                NativeLayoutTextPairer.Pair(
                    new[]
                    {
                        block
                    },
                    new[]
                    {
                        layout
                    }));

        var nativeEvidence =
            Assert.IsType<ComparableNativeTextEvidence>(
                pairing.ComparableNativeEvidence);

        var result =
            NativeOcrTextReconciler
                .ReconcileComparable(
                    new TextReconciliationInput(
                        405,
                        NativeTextStatus.Unverified,
                        nativeEvidence,
                        Ocr(
                            layout,
                            "After leaving Thessalonica,")),
                    nativeEvidence);

        Assert.Equal(
            TextReconciliationDecision.Agreement,
            result.Decision);

        Assert.Same(
            nativeEvidence.Extents.Single(),
            result.ComparableNativeExtent);

        Assert.Same(
            nativeEvidence,
            result.ComparableNativeEvidence);
    }

    [Fact]
    public void ReconcileComparable_UnverifiedConflictFailsClosed()
    {
        var layout =
            Layout(
                380,
                5,
                5,
                LayoutObservationKind.Text,
                0.07,
                0.42,
                0.40,
                0.94);

        var block =
            Block(
                2,
                2,
                Word(0, "conversion", 0.08, 0.43, 0.18, 0.46));

        var pairing =
            Assert.Single(
                NativeLayoutTextPairer.Pair(
                    new[]
                    {
                        block
                    },
                    new[]
                    {
                        layout
                    }));

        var nativeEvidence =
            Assert.IsType<ComparableNativeTextEvidence>(
                pairing.ComparableNativeEvidence);

        var result =
            NativeOcrTextReconciler
                .ReconcileComparable(
                    new TextReconciliationInput(
                        380,
                        NativeTextStatus.Unverified,
                        nativeEvidence,
                        Ocr(
                            layout,
                            "conversior")),
                    nativeEvidence);

        Assert.Equal(
            TextReconciliationDecision.Conflict,
            result.Decision);

        Assert.Equal(
            TextSelectionOrigin.None,
            result.SelectedOrigin);

        Assert.Null(
            result.SelectedText);

        Assert.False(
            result.TextsEquivalent);
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
        int sourceSequence,
        int readingOrder,
        params DocumentWord[] words) =>
        new(
            sourceSequence,
            readingOrder,
            string.Join(
                " ",
                words.Select(
                    word =>
                        word.Text)),
            new NormalizedRectangle(
                words.Min(word => word.Bounds.Left),
                words.Min(word => word.Bounds.Top),
                words.Max(word => word.Bounds.Right),
                words.Max(word => word.Bounds.Bottom)),
            words);

    private static LayoutObservation Layout(
        int physicalPageNumber,
        int observationSequence,
        int readingOrder,
        LayoutObservationKind kind,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            physicalPageNumber,
            observationSequence,
            readingOrder,
            kind,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            kind.ToString());

    private static OcrRegionResult Ocr(
        LayoutObservation layout,
        string text) =>
        new(
            "test-ocr",
            "test-profile-v1",
            layout,
            new[]
            {
                new OcrTextObservation(
                    layout.PhysicalPageNumber,
                    layout.ObservationSequence,
                    0,
                    text,
                    0.95,
                    layout.Bounds)
            });
}
