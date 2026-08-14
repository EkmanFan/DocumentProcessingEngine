using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Reconciliation;

public sealed class NativeOcrTextReconcilerTests
{
    [Fact]
    public void Reconcile_HealthyNativeWithoutOcr_SelectsNative()
    {
        var input =
            Input(
                NativeTextStatus.Healthy,
                Native("Native text"),
                ocr: null);

        var result =
            NativeOcrTextReconciler.Reconcile(input);

        Assert.Equal(
            TextReconciliationDecision.NativeOnly,
            result.Decision);
        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            result.SelectedOrigin);
        Assert.Equal(
            "Native text",
            result.SelectedText);
        Assert.Null(result.TextsEquivalent);
        Assert.True(result.IsResolved);
        Assert.False(result.HasDivergence);
    }

    [Fact]
    public void Reconcile_MissingNativeWithOcr_SelectsOcrInObservationOrder()
    {
        var input =
            Input(
                NativeTextStatus.Missing,
                native: null,
                Ocr(
                    (2, "world"),
                    (0, "Hello"),
                    (1, "targeted")));

        var result =
            NativeOcrTextReconciler.Reconcile(input);

        Assert.Equal(
            TextReconciliationDecision.OcrOnly,
            result.Decision);
        Assert.Equal(
            TextSelectionOrigin.Ocr,
            result.SelectedOrigin);
        Assert.Equal(
            "Hello targeted world",
            result.SelectedText);
        Assert.Equal(
            result.SelectedText,
            result.OcrText);
        Assert.True(result.IsResolved);
    }

    [Fact]
    public void Reconcile_MissingNativeAndEmptyOcr_IsExplicitlyUnresolved()
    {
        var result =
            NativeOcrTextReconciler.Reconcile(
                Input(
                    NativeTextStatus.Missing,
                    native: null,
                    Ocr()));

        Assert.Equal(
            TextReconciliationDecision.NoTextRecovered,
            result.Decision);
        Assert.Equal(
            TextSelectionOrigin.None,
            result.SelectedOrigin);
        Assert.Null(result.SelectedText);
        Assert.Null(result.OcrText);
        Assert.False(result.IsResolved);
    }

    [Fact]
    public void Reconcile_SuspiciousNativeWithoutOcr_DoesNotSilentlyTrustNative()
    {
        var result =
            NativeOcrTextReconciler.Reconcile(
                Input(
                    NativeTextStatus.Suspicious,
                    Native("Possibly incomplete native text"),
                    ocr: null));

        Assert.Equal(
            TextReconciliationDecision.SuspiciousNativeUnverified,
            result.Decision);
        Assert.Equal(
            TextSelectionOrigin.None,
            result.SelectedOrigin);
        Assert.Null(result.SelectedText);
        Assert.False(result.IsResolved);
    }

    [Fact]
    public void Reconcile_EquivalentNativeAndOcr_RecordsAgreementAndKeepsNative()
    {
        var nativeText =
            "Imagine,\nfor example, a letter.";

        var ocr =
            Ocr(
                (0, "Imagine,"),
                (1, "for example, a letter."));

        var result =
            NativeOcrTextReconciler.Reconcile(
                Input(
                    NativeTextStatus.Suspicious,
                    Native(nativeText),
                    ocr));

        Assert.Equal(
            TextReconciliationDecision.Agreement,
            result.Decision);
        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            result.SelectedOrigin);
        Assert.Equal(
            nativeText.Trim(),
            result.SelectedText);
        Assert.Equal(
            "Imagine, for example, a letter.",
            result.OcrText);
        Assert.True(result.TextsEquivalent);
        Assert.True(result.IsResolved);
        Assert.False(result.HasDivergence);
    }

    [Fact]
    public void Reconcile_HealthyNativeConflict_PrefersNativeButExposesDivergence()
    {
        var result =
            NativeOcrTextReconciler.Reconcile(
                Input(
                    NativeTextStatus.Healthy,
                    Native("The native reading is trusted."),
                    Ocr((0, "The OCR reading is different."))));

        Assert.Equal(
            TextReconciliationDecision.HealthyNativePreferred,
            result.Decision);
        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            result.SelectedOrigin);
        Assert.Equal(
            "The native reading is trusted.",
            result.SelectedText);
        Assert.False(result.TextsEquivalent);
        Assert.True(result.IsResolved);
        Assert.True(result.HasDivergence);
    }

    [Fact]
    public void Reconcile_SuspiciousNativeConflict_RemainsUnresolved()
    {
        var result =
            NativeOcrTextReconciler.Reconcile(
                Input(
                    NativeTextStatus.Suspicious,
                    Native("Native maybe wrong"),
                    Ocr((0, "OCR says something else"))));

        Assert.Equal(
            TextReconciliationDecision.Conflict,
            result.Decision);
        Assert.Equal(
            TextSelectionOrigin.None,
            result.SelectedOrigin);
        Assert.Null(result.SelectedText);
        Assert.False(result.TextsEquivalent);
        Assert.False(result.IsResolved);
        Assert.True(result.HasDivergence);
    }

    [Theory]
    [InlineData("alpha   beta", "alpha beta")]
    [InlineData("alpha\u00A0beta", "alpha beta")]
    [InlineData("of\u00ADfice", "office")]
    [InlineData("ﬁgure", "figure")]
    public void ConservativeComparison_NormalizesOnlyCompatibilityWhitespaceAndSoftHyphen(
        string left,
        string right)
    {
        Assert.True(
            NativeOcrTextReconciler.AreConservativelyEquivalent(
                left,
                right));
    }

    [Theory]
    [InlineData("Figure 11.1", "figure 11.1")]
    [InlineData("word,", "word")]
    [InlineData("colour", "color")]
    public void ConservativeComparison_DoesNotHideCasePunctuationOrSpellingDifferences(
        string left,
        string right)
    {
        Assert.False(
            NativeOcrTextReconciler.AreConservativelyEquivalent(
                left,
                right));
    }

    [Fact]
    public void Input_MissingStatusWithNativeBlock_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                Input(
                    NativeTextStatus.Missing,
                    Native("unexpected"),
                    ocr: null));
    }

    [Fact]
    public void Input_HealthyStatusWithoutNativeBlock_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                Input(
                    NativeTextStatus.Healthy,
                    native: null,
                    ocr: null));
    }

    [Fact]
    public void Input_OcrFromDifferentPage_IsRejected()
    {
        var ocr =
            Ocr(
                physicalPageNumber: 234,
                fragments: new[]
                {
                    (0, "wrong page")
                });

        Assert.Throws<ArgumentException>(
            () =>
                new TextReconciliationInput(
                    physicalPageNumber: 233,
                    NativeTextStatus.Missing,
                    nativeBlock: null,
                    ocr));
    }

    [Fact]
    public void Input_NonOverlappingNativeAndOcrRegions_AreRejected()
    {
        var native =
            new DocumentTextBlock(
                sourceSequence: 1,
                readingOrder: 1,
                text: "native",
                new NormalizedRectangle(
                    0.1,
                    0.1,
                    0.2,
                    0.2));

        var ocr =
            Ocr(
                bounds: new NormalizedRectangle(
                    0.7,
                    0.7,
                    0.8,
                    0.8),
                fragments: new[]
                {
                    (0, "ocr")
                });

        Assert.Throws<ArgumentException>(
            () =>
                new TextReconciliationInput(
                    physicalPageNumber: 233,
                    NativeTextStatus.Suspicious,
                    native,
                    ocr));
    }

    private static TextReconciliationInput Input(
        NativeTextStatus status,
        DocumentTextBlock? native,
        OcrRegionResult? ocr) =>
        new(
            physicalPageNumber: 233,
            status,
            native,
            ocr);

    private static DocumentTextBlock Native(
        string text) =>
        new(
            sourceSequence: 3,
            readingOrder: 3,
            text,
            new NormalizedRectangle(
                0.20,
                0.20,
                0.60,
                0.50));

    private static OcrRegionResult Ocr(
        params (int Sequence, string Text)[] fragments) =>
        Ocr(
            physicalPageNumber: 233,
            bounds: new NormalizedRectangle(
                0.25,
                0.25,
                0.55,
                0.45),
            fragments);

    private static OcrRegionResult Ocr(
        NormalizedRectangle bounds,
        params (int Sequence, string Text)[] fragments) =>
        Ocr(
            physicalPageNumber: 233,
            bounds,
            fragments);

    private static OcrRegionResult Ocr(
        int physicalPageNumber,
        (int Sequence, string Text)[] fragments) =>
        Ocr(
            physicalPageNumber,
            new NormalizedRectangle(
                0.25,
                0.25,
                0.55,
                0.45),
            fragments);

    private static OcrRegionResult Ocr(
        int physicalPageNumber,
        NormalizedRectangle bounds,
        params (int Sequence, string Text)[] fragments)
    {
        var layout =
            new LayoutObservation(
                physicalPageNumber,
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
                            physicalPageNumber,
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
