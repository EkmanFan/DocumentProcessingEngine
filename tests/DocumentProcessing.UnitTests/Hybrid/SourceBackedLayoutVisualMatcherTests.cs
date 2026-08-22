using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Engine.Hybrid;

namespace DocumentProcessing.UnitTests.Hybrid;

public sealed class SourceBackedLayoutVisualMatcherTests
{
    [Fact]
    public void AddSourceFigures_MultipleVisuals_AddsOneFigurePerPreservedSource()
    {
        var augmented =
            SourceBackedLayoutVisualMatcher
                .AddSourceFigures(
                    Plan(
                        Preserve(0),
                        Preserve(1)),
                    [
                        Source(
                            0,
                            0.20,
                            0.30,
                            0.40,
                            0.40),
                        Source(
                            1,
                            0.20,
                            0.60,
                            0.40,
                            0.70)
                    ],
                    Layout(
                        Text(
                            0,
                            0,
                            0.10,
                            0.10,
                            0.80,
                            0.20),
                        Text(
                            1,
                            1,
                            0.10,
                            0.80,
                            0.80,
                            0.90)));

        Assert.Equal(
            2,
            augmented.Observations.Count(
                observation =>
                    observation.RawLabel?.StartsWith(
                        "source_visual:",
                        StringComparison.Ordinal) ==
                    true));

        Assert.Equal(
            [
                "text",
                "source_visual:0",
                "source_visual:1",
                "text"
            ],
            augmented.Observations.Select(
                observation =>
                    observation.RawLabel));
    }

    [Fact]
    public void AddSourceFigures_IntersectingFormula_KeepsSourceAsPreservationUnit()
    {
        var source =
            Source(
                0,
                0.10,
                0.10,
                0.90,
                0.40);

        var augmented =
            SourceBackedLayoutVisualMatcher
                .AddSourceFigures(
                    Plan(
                        Preserve(0)),
                    [
                        source
                    ],
                    Layout(
                        Figure(
                            0,
                            0,
                            0.15,
                            0.12,
                            0.35,
                            0.20,
                            "formula"),
                        Figure(
                            1,
                            1,
                            0.40,
                            0.12,
                            0.70,
                            0.30,
                            "image"),
                        Figure(
                            2,
                            2,
                            0.72,
                            0.30,
                            0.85,
                            0.38,
                            "formula")));

        var sourceFigure =
            Assert.Single(
                augmented.Observations,
                observation =>
                    observation.RawLabel ==
                    "source_visual:0");

        Assert.Equal(
            source.EffectiveVisualBounds,
            sourceFigure.Bounds);

        Assert.Equal(
            4,
            augmented.Observations.Count);
    }

    [Fact]
    public void TryResolve_PpOnlyFormula_DoesNotCreateAdditionalVisual()
    {
        var source =
            Source(
                0,
                0.10,
                0.10,
                0.40,
                0.20);

        var augmented =
            SourceBackedLayoutVisualMatcher
                .AddSourceFigures(
                    Plan(
                        Preserve(0)),
                    [
                        source
                    ],
                    Layout(
                        Figure(
                            0,
                            0,
                            0.10,
                            0.60,
                            0.30,
                            0.65,
                            "formula")));

        var actual =
            SourceBackedLayoutVisualMatcher.TryResolve(
                Plan(
                    Preserve(0)),
                [
                    source
                ],
                augmented,
                out var resolved);

        Assert.True(
            actual);

        var evidence =
            Assert.Single(
                resolved);

        Assert.Equal(
            "source_visual:0",
            evidence.Observation.RawLabel);

        Assert.Equal(
            VisualEvidenceKind.SourceBackedMeaningfulVisual,
            evidence.Kind);
    }

    [Fact]
    public void TryResolve_MissingPlannedSourceFigure_FailsClosed()
    {
        var actual =
            SourceBackedLayoutVisualMatcher.TryResolve(
                Plan(
                    Preserve(0)),
                [
                    Source(
                        0,
                        0.10,
                        0.10,
                        0.40,
                        0.20)
                ],
                Layout(),
                out var resolved);

        Assert.False(
            actual);

        Assert.Empty(
            resolved);
    }

    [Fact]
    public void TryResolve_NoPlannedSourcePreservation_SucceedsWithNoSourceFigures()
    {
        var layout =
            Layout(
                Text(
                    0,
                    0,
                    0.10,
                    0.10,
                    0.90,
                    0.20));

        var actual =
            SourceBackedLayoutVisualMatcher
                .TryResolveWithSourceFigures(
                    Plan(),
                    [
                        Source(
                            0,
                            0,
                            0,
                            1,
                            1)
                    ],
                    layout,
                    out var executionLayout,
                    out var resolved);

        Assert.True(
            actual);

        Assert.Same(
            layout,
            executionLayout);

        Assert.Empty(
            resolved);
    }

    [Fact]
    public void TryResolve_AnalyzeVisualWithFormula_QualifiesSourceAsMeaningful()
    {
        var source =
            Source(
                0,
                0.10,
                0.10,
                0.90,
                0.40);

        var actual =
            SourceBackedLayoutVisualMatcher
                .TryResolveWithSourceFigures(
                    Plan(
                        Analyze(0)),
                    [source],
                    Layout(
                        Figure(
                            0,
                            0,
                            0.20,
                            0.15,
                            0.80,
                            0.35,
                            "formula")),
                    out _,
                    out var resolved);

        Assert.True(
            actual);

        var evidence =
            Assert.Single(
                resolved);

        Assert.Equal(
            VisualEvidenceKind.SourceBackedMeaningfulVisual,
            evidence.Kind);

        Assert.Equal(
            "source_visual:0",
            evidence.Observation.RawLabel);
    }

    [Fact]
    public void TryResolve_AnalyzeVisualWithoutStrongCategory_PreservesUnqualified()
    {
        var source =
            Source(
                0,
                0.10,
                0.10,
                0.90,
                0.40);

        var actual =
            SourceBackedLayoutVisualMatcher
                .TryResolveWithSourceFigures(
                    Plan(
                        Analyze(0)),
                    [source],
                    Layout(
                        Figure(
                            0,
                            0,
                            0.20,
                            0.15,
                            0.80,
                            0.35,
                            "image")),
                    out var executionLayout,
                    out var resolved);

        Assert.True(
            actual);

        var evidence =
            Assert.Single(
                resolved);

        Assert.Equal(
            VisualEvidenceKind.SourceBackedUnqualifiedVisual,
            evidence.Kind);

        Assert.Equal(
            "source_visual_unqualified:0",
            evidence.Observation.RawLabel);

        Assert.Equal(
            "source_visual_unqualified:0",
            executionLayout.Observations[^1].RawLabel);
    }

    [Fact]
    public void TryResolve_FirstPageFullCanvasWithDocumentTitle_ExcludesCover()
    {
        var source =
            Source(
                0,
                0,
                0,
                1,
                1);

        var actual =
            SourceBackedLayoutVisualMatcher
                .TryResolveWithSourceFigures(
                    Plan(
                        Analyze(0)),
                    [source],
                    new LayoutAnalysisResult(
                        "fake-layout",
                        physicalPageNumber:
                            1,
                        [
                            new LayoutObservation(
                                physicalPageNumber:
                                    1,
                                observationSequence:
                                    0,
                                readingOrder:
                                    0,
                                LayoutObservationKind.Heading,
                                new NormalizedRectangle(
                                    0.10,
                                    0.10,
                                    0.90,
                                    0.30),
                                "doc_title")
                        ]),
                    out var executionLayout,
                    out var resolved);

        Assert.True(
            actual);

        Assert.Empty(
            resolved);

        Assert.DoesNotContain(
            executionLayout.Observations,
            observation =>
                observation.RawLabel?.StartsWith(
                    "source_visual",
                    StringComparison.Ordinal) ==
                true);
    }

    private static PageExecutionPlan Plan(
        params VisualElementExecutionPlan[] visuals) =>
        new(
            physicalPageNumber:
                1,
            TextExecutionMode.NativeText,
            visuals);

    private static VisualElementExecutionPlan Preserve(
        int sourceVisualIndex) =>
        new(
            sourceVisualIndex,
            VisualExecutionAction.PreserveMeaningfulVisual);

    private static VisualElementExecutionPlan Analyze(
        int sourceVisualIndex) =>
        new(
            sourceVisualIndex,
            VisualExecutionAction.AnalyzeVisual);

    private static LayoutAnalysisResult Layout(
        params LayoutObservation[] observations) =>
        new(
            "fake-layout",
            physicalPageNumber:
                1,
            observations);

    private static LayoutObservation Text(
        int sequence,
        int readingOrder,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            physicalPageNumber:
                1,
            observationSequence:
                sequence,
            readingOrder,
            LayoutObservationKind.Text,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            "text");

    private static LayoutObservation Figure(
        int sequence,
        int readingOrder,
        double left,
        double top,
        double right,
        double bottom,
        string rawLabel) =>
        new(
            physicalPageNumber:
                1,
            observationSequence:
                sequence,
            readingOrder,
            LayoutObservationKind.Figure,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            rawLabel);

    private static VisualRasterObservation Source(
        int sourceVisualIndex,
        double left,
        double top,
        double right,
        double bottom)
    {
        var bounds =
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom);

        return new VisualRasterObservation(
            sourceVisualIndex,
            declaredPageBounds:
                bounds,
            VisualRasterDecodeSource.RawEmbeddedImage,
            pixelWidth:
                100,
            pixelHeight:
                100,
            backgroundUniformity:
                1,
            VisualForegroundState.Measured,
            foregroundPixelRatio:
                0.25,
            VisualPixelInteractionKind.NoForegroundWordIntersection,
            nativeWordsTouchedRatio:
                0,
            significantComponentCount:
                1,
            effectiveVisualBounds:
                bounds);
    }
}
