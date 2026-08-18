using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Hybrid.Normalization;
using DocumentProcessing.Engine.Hybrid.Segmentation;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class UnverifiedRouteSemanticParityTests
{
    [Fact]
    public void UnverifiedAgreement_PreservesNativeAuthoritativeText()
    {
        var native =
            Native(
                "Alpha   beta.");

        var reconciliation =
            NativeOcrTextReconciler
                .Reconcile(
                    new TextReconciliationInput(
                        physicalPageNumber:
                            1,
                        NativeTextStatus.Unverified,
                        native,
                        Ocr(
                            "Alpha beta.")));

        Assert.Equal(
            TextReconciliationDecision.Agreement,
            reconciliation.Decision);

        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            reconciliation.SelectedOrigin);

        Assert.Equal(
            native.Text,
            reconciliation.SelectedText);

        Assert.True(
            reconciliation.TextsEquivalent);

        Assert.True(
            reconciliation.IsResolved);

        Assert.False(
            reconciliation.HasDivergence);
    }

    [Fact]
    public void NativeReferenceAndUnverifiedAgreement_ConvergeAfterNormalizationAndSegmentation()
    {
        var native =
            Native(
                "Alpha   beta.");

        var nativeElement =
            HybridDocumentElementFactory
                .FromNative(
                    physicalPageNumber:
                        1,
                    native);

        var reconciliation =
            NativeOcrTextReconciler
                .Reconcile(
                    new TextReconciliationInput(
                        physicalPageNumber:
                            1,
                        NativeTextStatus.Unverified,
                        native,
                        Ocr(
                            "Alpha beta.")));

        var verifiedElement =
            HybridDocumentElementFactory
                .FromReconciliation(
                    reconciliation);

        Assert.Equal(
            nativeElement.Text,
            verifiedElement.Text);

        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            nativeElement.TextOrigin);

        Assert.Equal(
            TextSelectionOrigin.NativePdf,
            verifiedElement.TextOrigin);

        var nativeDocument =
            ProcessToSegmentation(
                nativeElement);

        var verifiedDocument =
            ProcessToSegmentation(
                verifiedElement);

        var nativeNormalizedText =
            Assert.Single(
                    nativeDocument
                        .Normalization
                        .Pages)
                .Elements
                .Single()
                .Text;

        var verifiedNormalizedText =
            Assert.Single(
                    verifiedDocument
                        .Normalization
                        .Pages)
                .Elements
                .Single()
                .Text;

        Assert.Equal(
            nativeNormalizedText,
            verifiedNormalizedText);

        var nativeSegment =
            Assert.Single(
                nativeDocument
                    .Segmentation
                    .Segments);

        var verifiedSegment =
            Assert.Single(
                verifiedDocument
                    .Segmentation
                    .Segments);

        Assert.Equal(
            nativeSegment.Text,
            verifiedSegment.Text);

        Assert.Equal(
            "Alpha beta.",
            verifiedSegment.Text);

        Assert.Equal(
            new[]
            {
                TextSelectionOrigin.NativePdf
            },
            verifiedSegment.TextOrigins);

        Assert.Equal(
            TextReconciliationDecision.Agreement,
            verifiedElement
                .Reconciliation!
                .Decision);
    }

    [Fact]
    public void UnverifiedConflict_RemainsExplicitlyUnresolved()
    {
        var native =
            Native(
                "The native reading.");

        var reconciliation =
            NativeOcrTextReconciler
                .Reconcile(
                    new TextReconciliationInput(
                        physicalPageNumber:
                            1,
                        NativeTextStatus.Unverified,
                        native,
                        Ocr(
                            "A different OCR reading.")));

        Assert.Equal(
            TextReconciliationDecision.Conflict,
            reconciliation.Decision);

        Assert.Equal(
            TextSelectionOrigin.None,
            reconciliation.SelectedOrigin);

        Assert.Null(
            reconciliation.SelectedText);

        Assert.False(
            reconciliation.TextsEquivalent);

        Assert.False(
            reconciliation.IsResolved);

        Assert.True(
            reconciliation.HasDivergence);

        var element =
            HybridDocumentElementFactory
                .FromReconciliation(
                    reconciliation);

        Assert.Equal(
            HybridDocumentElementKind.UnresolvedText,
            element.Kind);

        Assert.False(
            element.HasAuthoritativeText);

        Assert.Null(
            element.Text);
    }

    [Fact]
    public void UnverifiedWithoutUsableOcr_DoesNotSilentlyTrustNative()
    {
        var reconciliation =
            NativeOcrTextReconciler
                .Reconcile(
                    new TextReconciliationInput(
                        physicalPageNumber:
                            1,
                        NativeTextStatus.Unverified,
                        Native(
                            "Native text requiring verification."),
                        ocrRegion:
                            null));

        Assert.Equal(
            TextSelectionOrigin.None,
            reconciliation.SelectedOrigin);

        Assert.Null(
            reconciliation.SelectedText);

        Assert.False(
            reconciliation.IsResolved);
    }

    private static (
        DocumentProcessing.Core.Hybrid.Normalization.HybridDocumentNormalizationResult Normalization,
        DocumentProcessing.Core.Hybrid.Segmentation.HybridDocumentSegmentationResult Segmentation)
        ProcessToSegmentation(
            HybridDocumentElement element)
    {
        var page =
            HybridDocumentAssembler
                .AssemblePage(
                    physicalPageNumber:
                        1,
                    [
                        element
                    ]);

        var assembly =
            HybridDocumentAssembler
                .AssembleDocument(
                    [
                        page
                    ]);

        var normalization =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly);

        var segmentation =
            new HybridDocumentSegmenter()
                .Segment(
                    normalization);

        return (
            normalization,
            segmentation);
    }

    private static DocumentTextBlock Native(
        string text) =>
        new(
            sourceSequence:
                0,
            readingOrder:
                0,
            text,
            new NormalizedRectangle(
                0.10,
                0.20,
                0.90,
                0.40));

    private static OcrRegionResult Ocr(
        string text)
    {
        var bounds =
            new NormalizedRectangle(
                0.10,
                0.20,
                0.90,
                0.40);

        var layout =
            new LayoutObservation(
                physicalPageNumber:
                    1,
                observationSequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Text,
                bounds,
                rawLabel:
                    "text");

        return new OcrRegionResult(
            "test-ocr",
            "test-ocr-v1",
            layout,
            [
                new OcrTextObservation(
                    physicalPageNumber:
                        1,
                    sourceLayoutObservationSequence:
                        0,
                    observationSequence:
                        0,
                    text,
                    confidence:
                        0.99,
                    bounds)
            ]);
    }
}
