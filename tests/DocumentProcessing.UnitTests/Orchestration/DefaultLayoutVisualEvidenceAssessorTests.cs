using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Engine.Orchestration;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class DefaultLayoutVisualEvidenceAssessorTests
{
    [Fact]
    public void Assess_P233FigureWithStrongCaption_ClassifiesCaptionedMeaningfulVisual()
    {
        var figure =
            Figure(
                sequence:
                    4,
                readingOrder:
                    4,
                new NormalizedRectangle(
                    0.2426,
                    0.4363,
                    0.5716,
                    0.8593));

        var caption =
            Caption(
                sequence:
                    5,
                readingOrder:
                    5,
                new NormalizedRectangle(
                    0.2379,
                    0.8714,
                    0.5583,
                    0.9419));

        var evidence =
            Assert.Single(
                new DefaultLayoutVisualEvidenceAssessor()
                    .Assess(
                        Layout(
                            figure,
                            caption)));

        Assert.Equal(
            figure,
            evidence.Observation);

        Assert.Equal(
            VisualEvidenceKind.CaptionedMeaningfulVisual,
            evidence.Kind);
    }

    [Fact]
    public void Assess_FigureWithoutCaption_FailsClosedToUnknown()
    {
        var figure =
            Figure(
                sequence:
                    2,
                readingOrder:
                    2,
                new NormalizedRectangle(
                    0.10,
                    0.40,
                    0.90,
                    0.80));

        var evidence =
            Assert.Single(
                new DefaultLayoutVisualEvidenceAssessor()
                    .Assess(
                        Layout(
                            figure)));

        Assert.Equal(
            VisualEvidenceKind.Unknown,
            evidence.Kind);
    }

    [Fact]
    public void Assess_NoFigure_ProducesNoLayoutVisualEvidence()
    {
        var actual =
            new DefaultLayoutVisualEvidenceAssessor()
                .Assess(
                    Layout(
                        new LayoutObservation(
                            1,
                            observationSequence:
                                0,
                            readingOrder:
                                0,
                            LayoutObservationKind.Text,
                            new NormalizedRectangle(
                                0.10,
                                0.10,
                                0.90,
                                0.30),
                            "text")));

        Assert.Empty(
            actual);
    }

    [Fact]
    public void Assess_FigureWithTwoStrongCaptions_FailsClosedToUnknown()
    {
        var figure =
            Figure(
                sequence:
                    2,
                readingOrder:
                    2,
                new NormalizedRectangle(
                    0.20,
                    0.30,
                    0.80,
                    0.70));

        var firstCaption =
            Caption(
                sequence:
                    3,
                readingOrder:
                    3,
                new NormalizedRectangle(
                    0.20,
                    0.71,
                    0.80,
                    0.76));

        var secondCaption =
            Caption(
                sequence:
                    4,
                readingOrder:
                    4,
                new NormalizedRectangle(
                    0.25,
                    0.72,
                    0.75,
                    0.78));

        var evidence =
            Assert.Single(
                new DefaultLayoutVisualEvidenceAssessor()
                    .Assess(
                        Layout(
                            figure,
                            firstCaption,
                            secondCaption)));

        Assert.Equal(
            VisualEvidenceKind.Unknown,
            evidence.Kind);
    }

    [Fact]
    public void Assess_FigureWithDistantCaption_FailsClosedToUnknown()
    {
        var figure =
            Figure(
                sequence:
                    2,
                readingOrder:
                    2,
                new NormalizedRectangle(
                    0.20,
                    0.20,
                    0.80,
                    0.50));

        var caption =
            Caption(
                sequence:
                    3,
                readingOrder:
                    3,
                new NormalizedRectangle(
                    0.20,
                    0.70,
                    0.80,
                    0.76));

        var evidence =
            Assert.Single(
                new DefaultLayoutVisualEvidenceAssessor()
                    .Assess(
                        Layout(
                            figure,
                            caption)));

        Assert.Equal(
            VisualEvidenceKind.Unknown,
            evidence.Kind);
    }

    [Fact]
    public void LayoutVisualEvidence_RejectsNonFigureObservation()
    {
        var text =
            new LayoutObservation(
                1,
                observationSequence:
                    0,
                readingOrder:
                    0,
                LayoutObservationKind.Text,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.90,
                    0.30),
                "text");

        Assert.Throws<ArgumentException>(
            () =>
                new LayoutVisualEvidence(
                    text,
                    VisualEvidenceKind.Unknown));
    }

    private static LayoutAnalysisResult Layout(
        params LayoutObservation[] observations) =>
        new(
            "fake-layout",
            physicalPageNumber:
                1,
            observations);

    private static LayoutObservation Figure(
        int sequence,
        int? readingOrder,
        NormalizedRectangle bounds) =>
        new(
            1,
            sequence,
            readingOrder,
            LayoutObservationKind.Figure,
            bounds,
            "image");

    private static LayoutObservation Caption(
        int sequence,
        int? readingOrder,
        NormalizedRectangle bounds) =>
        new(
            1,
            sequence,
            readingOrder,
            LayoutObservationKind.Caption,
            bounds,
            "figure_title");
}
