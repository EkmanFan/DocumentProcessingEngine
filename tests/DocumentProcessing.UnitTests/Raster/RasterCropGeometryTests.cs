using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.UnitTests.Raster;

public sealed class RasterCropGeometryTests
{
    [Fact]
    public void FromNormalized_ClampsOnlyThePhysicalCropToRasterBounds()
    {
        var crop =
            RasterCropGeometry.FromNormalized(
                new NormalizedRectangle(
                    -0.25,
                    -0.10,
                    1.20,
                    1.50),
                pagePixelWidth: 1000,
                pagePixelHeight: 2000);

        Assert.Equal(
            new PixelRectangle(
                0,
                0,
                1000,
                2000),
            crop);
    }

    [Fact]
    public void FromNormalized_UsesFloorForOriginAndCeilingForExtent()
    {
        var crop =
            RasterCropGeometry.FromNormalized(
                new NormalizedRectangle(
                    0.1001,
                    0.2001,
                    0.4001,
                    0.3001),
                pagePixelWidth: 1000,
                pagePixelHeight: 1000);

        Assert.Equal(
            new PixelRectangle(
                100,
                200,
                401,
                301),
            crop);
    }

    [Fact]
    public void FromNormalized_FullyOutsideRaster_Throws()
    {
        Assert.Throws<InvalidDataException>(
            () =>
                RasterCropGeometry.FromNormalized(
                    new NormalizedRectangle(
                        1.1,
                        0.1,
                        1.2,
                        0.2),
                    pagePixelWidth: 1000,
                    pagePixelHeight: 2000));
    }
}
