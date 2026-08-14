using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Engine.Visual;

namespace DocumentProcessing.UnitTests.Visual;

public sealed class VisualPreservationPlannerTests
{
    [Fact]
    public void Create_EhrmanRepresentativeLayout_OnlyPlansPreserveVisualRegions()
    {
        var observations =
            new[]
            {
                Observation(
                    0,
                    LayoutObservationKind.Unknown,
                    0.01,
                    0.01,
                    0.10,
                    0.05),
                Observation(
                    2,
                    LayoutObservationKind.Heading,
                    0.24,
                    0.24,
                    0.54,
                    0.30),
                Observation(
                    3,
                    LayoutObservationKind.Text,
                    0.24,
                    0.32,
                    0.57,
                    0.42),
                Observation(
                    4,
                    LayoutObservationKind.Figure,
                    0.236697,
                    0.429652,
                    0.582942,
                    0.865355),
                Observation(
                    5,
                    LayoutObservationKind.Caption,
                    0.24,
                    0.87,
                    0.56,
                    0.94)
            };

        var layout =
            new LayoutAnalysisResult(
                "pp-structurev3",
                233,
                observations);

        var plan =
            VisualPreservationPlanner.Create(
                layout,
                2556,
                3305);

        var target =
            Assert.Single(plan);

        Assert.Equal(
            4,
            target.SourceLayoutObservation.ObservationSequence);
        Assert.Equal(
            LayoutObservationKind.Figure,
            target.SourceLayoutObservation.Kind);
        Assert.True(target.Crop.Width > 0);
        Assert.True(target.Crop.Height > 0);
    }

    [Fact]
    public void Create_NoPreserveVisualRegions_ReturnsEmptyPlan()
    {
        var layout =
            new LayoutAnalysisResult(
                "pp-structurev3",
                233,
                new[]
                {
                    Observation(
                        0,
                        LayoutObservationKind.Text,
                        0.1,
                        0.1,
                        0.5,
                        0.2),
                    Observation(
                        1,
                        LayoutObservationKind.Unknown,
                        0.1,
                        0.3,
                        0.5,
                        0.4)
                });

        var plan =
            VisualPreservationPlanner.Create(
                layout,
                1000,
                2000);

        Assert.Empty(plan);
    }

    private static LayoutObservation Observation(
        int sequence,
        LayoutObservationKind kind,
        double left,
        double top,
        double right,
        double bottom) =>
        new(
            physicalPageNumber: 233,
            observationSequence: sequence,
            readingOrder: sequence,
            kind,
            new NormalizedRectangle(
                left,
                top,
                right,
                bottom),
            rawLabel: kind.ToString());
}
