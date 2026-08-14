using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Engine.Normalization;

/// <summary>
/// Shared deterministic positional policy for recurring header/footer
/// candidates. Evidence bounds remain in canonical page coordinates while the
/// position test is evaluated relative to the producer's effective content
/// viewport.
/// </summary>
internal static class RecurringMarginGeometry
{
    private const double HeaderZoneFraction =
        0.12;

    private const double FooterZoneFraction =
        0.12;

    private const double MaximumMarginHeightFraction =
        0.20;

    public static RecurringMarginZone? GetZone(
        NormalizedRectangle bounds,
        NormalizedRectangle contentViewport)
    {
        var viewportHeight =
            contentViewport.Bottom -
            contentViewport.Top;

        if (viewportHeight <= 0)
        {
            throw new ArgumentException(
                "Content viewport must have positive height.",
                nameof(contentViewport));
        }

        var relativeTop =
            (bounds.Top -
             contentViewport.Top) /
            viewportHeight;

        var relativeBottom =
            (bounds.Bottom -
             contentViewport.Top) /
            viewportHeight;

        var relativeHeight =
            (bounds.Bottom -
             bounds.Top) /
            viewportHeight;

        if (relativeHeight <= 0 ||
            relativeHeight >
            MaximumMarginHeightFraction)
        {
            return null;
        }

        if (relativeTop <=
            HeaderZoneFraction)
        {
            return RecurringMarginZone.Header;
        }

        if (relativeBottom >=
            1.0 -
            FooterZoneFraction)
        {
            return RecurringMarginZone.Footer;
        }

        return null;
    }
}

internal enum RecurringMarginZone
{
    Header = 0,
    Footer = 1
}
