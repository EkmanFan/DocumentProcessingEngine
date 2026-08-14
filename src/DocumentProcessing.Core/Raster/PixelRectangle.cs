namespace DocumentProcessing.Core.Raster;

/// <summary>
/// Integer rectangle in a raster pixel coordinate space.
///
/// The rectangle uses half-open semantics:
/// [Left, Right) x [Top, Bottom).
/// </summary>
public readonly record struct PixelRectangle
{
    public PixelRectangle(
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
}
