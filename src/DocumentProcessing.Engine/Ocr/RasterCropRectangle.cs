using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Engine.Ocr;

/// <summary>
/// Integer pixel rectangle used to crop an actual page raster.
///
/// Source evidence remains unclamped in NormalizedRectangle. Clamping occurs
/// only here, at the physical raster boundary, because a crop cannot address
/// pixels outside the raster.
/// </summary>
public readonly record struct RasterCropRectangle
{
    public RasterCropRectangle(
        int left,
        int top,
        int right,
        int bottom)
    {
        if (left < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(left));
        }

        if (top < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(top));
        }

        if (right <= left)
        {
            throw new ArgumentException(
                "Right must be greater than left.",
                nameof(right));
        }

        if (bottom <= top)
        {
            throw new ArgumentException(
                "Bottom must be greater than top.",
                nameof(bottom));
        }

        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public int Left { get; }

    public int Top { get; }

    public int Right { get; }

    public int Bottom { get; }

    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public static RasterCropRectangle FromNormalized(
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

        return new RasterCropRectangle(
            left,
            top,
            right,
            bottom);
    }
}
