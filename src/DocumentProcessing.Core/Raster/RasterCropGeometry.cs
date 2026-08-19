using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Raster;

/// <summary>
/// Converts normalized page evidence into the exact pixel rectangle that can be
/// physically addressed on a raster.
///
/// Normalized source evidence is intentionally not mutated or clamped. Clamping
/// occurs only at this physical raster boundary.
/// </summary>
public static class RasterCropGeometry
{
    public static PixelRectangle FromNormalized(
        NormalizedRectangle bounds,
        int pagePixelWidth,
        int pagePixelHeight)
    {
        if (pagePixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pagePixelWidth));
        }

        if (pagePixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pagePixelHeight));
        }

        var left =
            (int)Math.Floor(
                Math.Clamp(bounds.Left, 0d, 1d) *
                pagePixelWidth);
        var top =
            (int)Math.Floor(
                Math.Clamp(bounds.Top, 0d, 1d) *
                pagePixelHeight);
        var right =
            (int)Math.Ceiling(
                Math.Clamp(bounds.Right, 0d, 1d) *
                pagePixelWidth);
        var bottom =
            (int)Math.Ceiling(
                Math.Clamp(bounds.Bottom, 0d, 1d) *
                pagePixelHeight);

        if (right <= left ||
            bottom <= top)
        {
            throw new InvalidDataException(
                "Layout region does not intersect the page raster with " +
                "a non-empty crop.");
        }

        return new PixelRectangle(
            left,
            top,
            right,
            bottom);
    }
}
