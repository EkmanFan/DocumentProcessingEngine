using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.UnitTests.Extraction;

public sealed class NormalizedRectangleTests
{
    [Fact]
    public void Constructor_PreservesCoordinates()
    {
        var rectangle = new NormalizedRectangle(0.1, 0.2, 0.3, 0.4);

        Assert.Equal(0.1, rectangle.Left);
        Assert.Equal(0.2, rectangle.Top);
        Assert.Equal(0.3, rectangle.Right);
        Assert.Equal(0.4, rectangle.Bottom);
    }

    [Fact]
    public void Constructor_PreservesFiniteCoordinatesOutsideVisiblePage()
    {
        var rectangle =
            new NormalizedRectangle(
                -0.1,
                -0.2,
                1.1,
                1.2);

        Assert.Equal(-0.1, rectangle.Left);
        Assert.Equal(-0.2, rectangle.Top);
        Assert.Equal(1.1, rectangle.Right);
        Assert.Equal(1.2, rectangle.Bottom);
    }

    [Fact]
    public void ValueEquality_UsesCoordinates()
    {
        var first =
            new NormalizedRectangle(
                0.1,
                0.2,
                0.3,
                0.4);

        var second =
            new NormalizedRectangle(
                0.1,
                0.2,
                0.3,
                0.4);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Constructor_RejectsNonFiniteCoordinates()
    {
        var leftException =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new NormalizedRectangle(
                    double.NaN,
                    0,
                    1,
                    1));

        var rightException =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new NormalizedRectangle(
                    0,
                    0,
                    double.PositiveInfinity,
                    1));

        Assert.Equal("left", leftException.ParamName);
        Assert.Equal("right", rightException.ParamName);
    }

    [Fact]
    public void Constructor_RejectsInvertedBounds()
    {
        Assert.Throws<ArgumentException>(
            () => new NormalizedRectangle(0.7, 0.2, 0.3, 0.4));

        Assert.Throws<ArgumentException>(
            () => new NormalizedRectangle(0.1, 0.8, 0.3, 0.4));
    }
}
