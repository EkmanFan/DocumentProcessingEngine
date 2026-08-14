using DocumentProcessing.Core.Layout;
using DocumentProcessing.Engine.Layout;

namespace DocumentProcessing.Engine.Ocr;

/// <summary>
/// Converts neutral layout evidence into a deterministic OCR work plan.
///
/// Only regions whose LayoutTreatment is RecognizeText are returned. Figure,
/// Table, Unknown, and any future unrecognized layout kinds are excluded unless
/// the deterministic policy is deliberately changed.
/// </summary>
public static class TargetedOcrPlanner
{
    public static IReadOnlyList<TargetedOcrRegion> Create(
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
            new List<TargetedOcrRegion>();

        foreach (var observation in layoutResult.Observations)
        {
            if (LayoutTreatmentPolicy.Decide(observation.Kind) !=
                LayoutTreatment.RecognizeText)
            {
                continue;
            }

            result.Add(
                new TargetedOcrRegion(
                    observation,
                    RasterCropRectangle.FromNormalized(
                        observation.Bounds,
                        pagePixelWidth,
                        pagePixelHeight)));
        }

        return result;
    }
}
