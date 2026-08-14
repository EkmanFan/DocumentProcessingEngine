namespace DocumentProcessing.Core.Extraction;

/// <summary>
/// Rectangle expressed in a normalized page coordinate space.
/// The canonical origin is the top-left corner of the page.
///
/// Coordinates are relative to page width and height and are intentionally not
/// clamped to [0, 1], so source evidence outside the visible page is not
/// silently discarded.
/// </summary>
public readonly record struct NormalizedRectangle
{
    public NormalizedRectangle(
        double left,
        double top,
        double right,
        double bottom)
    {
        ValidateFinite(left, nameof(left));
        ValidateFinite(top, nameof(top));
        ValidateFinite(right, nameof(right));
        ValidateFinite(bottom, nameof(bottom));

        if (right < left)
        {
            throw new ArgumentException(
                "Right must be greater than or equal to left.",
                nameof(right));
        }

        if (bottom < top)
        {
            throw new ArgumentException(
                "Bottom must be greater than or equal to top.",
                nameof(bottom));
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

    private static void ValidateFinite(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Rectangle coordinate must be finite.");
        }
    }
}
