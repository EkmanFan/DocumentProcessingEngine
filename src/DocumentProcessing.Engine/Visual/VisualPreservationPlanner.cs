using DocumentProcessing.Core.Layout;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Raster;

namespace DocumentProcessing.Engine.Visual;

/// <summary>
/// Converts neutral layout evidence into a deterministic visual-preservation
/// plan.
///
/// A region is selected only when LayoutTreatmentPolicy explicitly returns
/// PreserveVisualWithoutOcr.
/// </summary>
public static class VisualPreservationPlanner
{
    public static IReadOnlyList<VisualPreservationTarget> Create(
        LayoutAnalysisResult layoutResult,
        int pagePixelWidth,
        int pagePixelHeight)
    {
        ArgumentNullException.ThrowIfNull(layoutResult);

        if (pagePixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pagePixelWidth));
        }

        if (pagePixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pagePixelHeight));
        }

        var result =
            new List<VisualPreservationTarget>();

        foreach (var observation in layoutResult.Observations)
        {
            if (LayoutTreatmentPolicy.Decide(observation.Kind) !=
                LayoutTreatment.PreserveVisualWithoutOcr)
            {
                continue;
            }

            result.Add(
                new VisualPreservationTarget(
                    observation,
                    RasterCropGeometry.FromNormalized(
                        observation.Bounds,
                        pagePixelWidth,
                        pagePixelHeight)));
        }

        return result;
    }
}
