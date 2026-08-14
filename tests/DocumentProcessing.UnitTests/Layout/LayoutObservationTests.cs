using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.UnitTests.Layout;

public sealed class LayoutObservationTests
{
    [Fact]
    public void Constructor_KeepsObservationSequenceIndependentFromReadingOrder()
    {
        var bounds =
            new NormalizedRectangle(
                0.1,
                0.2,
                0.8,
                0.4);

        var observation =
            new LayoutObservation(
                physicalPageNumber: 7,
                observationSequence: 11,
                readingOrder: 3,
                LayoutObservationKind.Figure,
                bounds,
                rawLabel: " image ");

        Assert.Equal(7, observation.PhysicalPageNumber);
        Assert.Equal(11, observation.ObservationSequence);
        Assert.Equal(3, observation.ReadingOrder);
        Assert.Equal(LayoutObservationKind.Figure, observation.Kind);
        Assert.Equal(bounds, observation.Bounds);
        Assert.Equal("image", observation.RawLabel);
    }

    [Fact]
    public void Constructor_AllowsUnknownReadingOrderAndRawLabel()
    {
        var observation =
            new LayoutObservation(
                1,
                0,
                null,
                LayoutObservationKind.Unknown,
                new NormalizedRectangle(0, 0, 1, 1));

        Assert.Null(observation.ReadingOrder);
        Assert.Null(observation.RawLabel);
    }

    [Fact]
    public void Constructor_RejectsInvalidValues()
    {
        var bounds =
            new NormalizedRectangle(
                0.1,
                0.2,
                0.8,
                0.4);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LayoutObservation(
                0,
                0,
                0,
                LayoutObservationKind.Text,
                bounds));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LayoutObservation(
                1,
                -1,
                0,
                LayoutObservationKind.Text,
                bounds));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LayoutObservation(
                1,
                0,
                -1,
                LayoutObservationKind.Text,
                bounds));
    }

    [Fact]
    public void LayoutAnalysisResult_CopiesInputObservations()
    {
        var source =
            new List<LayoutObservation>
            {
                new(
                    1,
                    0,
                    0,
                    LayoutObservationKind.Text,
                    new NormalizedRectangle(0, 0, 1, 1))
            };

        var result =
            new LayoutAnalysisResult(
                "test-backend",
                1,
                source);

        source.Clear();

        Assert.Single(result.Observations);
    }

    [Fact]
    public void LayoutAnalysisResult_RejectsObservationsFromAnotherPage()
    {
        var observation =
            new LayoutObservation(
                2,
                0,
                0,
                LayoutObservationKind.Text,
                new NormalizedRectangle(0, 0, 1, 1));

        Assert.Throws<ArgumentException>(
            () => new LayoutAnalysisResult(
                "test-backend",
                1,
                [observation]));
    }
}
