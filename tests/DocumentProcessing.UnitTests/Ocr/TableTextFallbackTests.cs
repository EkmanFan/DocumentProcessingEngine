using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.UnitTests.Ocr;

public sealed class TableTextFallbackTests
{
    [Fact]
    public void Planner_AuthorizesTableTextButStillDefersUnknownAndExcludesFigure()
    {
        var table =
            Observation(
                sequence:
                    0,
                LayoutObservationKind.Table,
                left:
                    0.10,
                top:
                    0.10,
                right:
                    0.90,
                bottom:
                    0.80);

        var unknown =
            Observation(
                sequence:
                    1,
                LayoutObservationKind.Unknown,
                left:
                    0.10,
                top:
                    0.81,
                right:
                    0.90,
                bottom:
                    0.86);

        var figure =
            Observation(
                sequence:
                    2,
                LayoutObservationKind.Figure,
                left:
                    0.10,
                top:
                    0.87,
                right:
                    0.90,
                bottom:
                    0.98);

        var layout =
            new LayoutAnalysisResult(
                "pp-structurev3",
                14,
                new[]
                {
                    table,
                    unknown,
                    figure
                });

        var plan =
            TargetedOcrPlanner.Create(
                layout,
                2556,
                3305);

        var target =
            Assert.Single(
                plan);

        Assert.Same(
            table,
            target.SourceLayoutObservation);

        Assert.True(
            LayoutTextPolicy.IsTextRecognitionCandidate(
                LayoutObservationKind.Table));

        Assert.False(
            LayoutTextPolicy.IsTextRecognitionCandidate(
                LayoutObservationKind.Unknown));

        Assert.False(
            LayoutTextPolicy.IsTextRecognitionCandidate(
                LayoutObservationKind.Figure));
    }

    [Fact]
    public void ReconciledTableOcr_BecomesTextFlowAndRetainsTableProvenance()
    {
        var table =
            Observation(
                sequence:
                    0,
                LayoutObservationKind.Table,
                left:
                    0.10,
                top:
                    0.10,
                right:
                    0.90,
                bottom:
                    0.80);

        var ocrObservation =
            new OcrTextObservation(
                physicalPageNumber:
                    14,
                sourceLayoutObservationSequence:
                    table.ObservationSequence,
                observationSequence:
                    0,
                text:
                    "Chapter One 23",
                confidence:
                    0.91,
                bounds:
                    table.Bounds);

        var ocrRegion =
            new OcrRegionResult(
                "paddleocr",
                "table-text-fallback-test-v1",
                table,
                new[]
                {
                    ocrObservation
                });

        var reconciliation =
            NativeOcrTextReconciler
                .Reconcile(
                    new TextReconciliationInput(
                        physicalPageNumber:
                            14,
                        NativeTextStatus.Missing,
                        nativeBlock:
                            null,
                        ocrRegion));

        Assert.True(
            reconciliation.IsResolved);

        Assert.Equal(
            TextReconciliationDecision.OcrOnly,
            reconciliation.Decision);

        var element =
            HybridDocumentElementFactory
                .FromReconciliation(
                    reconciliation);

        Assert.Equal(
            HybridDocumentElementKind.Text,
            element.Kind);

        Assert.Equal(
            TextSelectionOrigin.Ocr,
            element.TextOrigin);

        Assert.Equal(
            "Chapter One 23",
            element.Text);

        Assert.Same(
            table,
            element.LayoutObservation);

        Assert.Equal(
            LayoutObservationKind.Table,
            element.LayoutObservation!.Kind);

        Assert.True(
            element.HasAuthoritativeText);
    }

    private static LayoutObservation Observation(
        int sequence,
        LayoutObservationKind kind,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            physicalPageNumber:
                14,
            observationSequence:
                sequence,
            readingOrder:
                sequence,
            kind,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            rawLabel:
                kind.ToString());
}
