using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Engine.Ocr;

namespace DocumentProcessing.UnitTests.Ocr;

public sealed class TargetedOcrPlannerTests
{
    [Fact]
    public void Create_EhrmanRepresentativeLayout_OnlyPlansRecognizeTextRegions()
    {
        var observations =
            new[]
            {
                Observation(
                    0,
                    LayoutObservationKind.Unknown,
                    0.01,
                    0.01,
                    0.1,
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
                    0.24,
                    0.44,
                    0.57,
                    0.86),
                Observation(
                    5,
                    LayoutObservationKind.Caption,
                    0.24,
                    0.87,
                    0.56,
                    0.94),
                Observation(
                    6,
                    LayoutObservationKind.Text,
                    0.60,
                    0.23,
                    0.93,
                    0.41)
            };

        var layout =
            new LayoutAnalysisResult(
                "pp-structurev3",
                233,
                observations);

        var plan =
            TargetedOcrPlanner.Create(
                layout,
                2556,
                3305);

        Assert.Equal(
            new[] { 2, 3, 5, 6 },
            plan
                .Select(
                    region =>
                        region.SourceLayoutObservation.ObservationSequence)
                .ToArray());

        Assert.DoesNotContain(
            plan,
            region =>
                region.SourceLayoutObservation.Kind ==
                LayoutObservationKind.Figure);

        Assert.All(
            plan,
            region =>
            {
                Assert.True(region.Crop.Width > 0);
                Assert.True(region.Crop.Height > 0);
            });
    }

    [Fact]
    public void FromNormalized_ClampsOnlyThePhysicalCropToRasterBounds()
    {
        var crop =
            RasterCropRectangle.FromNormalized(
                new NormalizedRectangle(
                    -0.25,
                    -0.10,
                    1.20,
                    1.50),
                pagePixelWidth: 1000,
                pagePixelHeight: 2000);

        Assert.Equal(
            new RasterCropRectangle(
                0,
                0,
                1000,
                2000),
            crop);
    }

    [Fact]
    public void FromNormalized_FullyOutsideRaster_Throws()
    {
        Assert.Throws<InvalidDataException>(
            () =>
                RasterCropRectangle.FromNormalized(
                    new NormalizedRectangle(
                        1.1,
                        0.1,
                        1.2,
                        0.2),
                    pagePixelWidth: 1000,
                    pagePixelHeight: 2000));
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
