namespace DocumentProcessing.Core.Extraction;

/// <summary>
/// Rectangle expressed in a normalized page coordinate space.
/// The canonical origin is the top-left corner of the page.
/// Coordinates are relative to page width and height and are not clamped,
/// so source evidence outside the visible page is not silently discarded.
/// </summary>
public sealed record NormalizedRectangle
{
    public NormalizedRectangle(double left, double top, double right, double bottom)
    {
        if (!double.IsFinite(left) ||
            !double.IsFinite(top) ||
            !double.IsFinite(right) ||
            !double.IsFinite(bottom))
        {
            throw new ArgumentOutOfRangeException(
                nameof(left),
                "Rectangle coordinates must be finite.");
        }

        if (right < left)
        {
            throw new ArgumentException("Right must be greater than or equal to left.");
        }

        if (bottom < top)
        {
            throw new ArgumentException("Bottom must be greater than or equal to top.");
        }

        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public double Left { get; }
    public double Top { get; }
    public double Right { get; }
    public double Bottom { get; }
}
