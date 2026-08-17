using DocumentProcessing.Core.Layout;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Raster;

namespace DocumentProcessing.Engine.Ocr;

/// <summary>
/// Converts neutral layout evidence into a deterministic OCR work plan.
///
/// Only regions authorized by <see cref="LayoutTextPolicy"/> are returned.
/// Text, Heading, Caption, and Table are OCR-authorized. Figure, Unknown, and
/// future unrecognized kinds remain excluded unless the text policy is
/// deliberately changed.
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
            if (!LayoutTextPolicy.IsTextRecognitionCandidate(
                    observation.Kind))
            {
                continue;
            }

            result.Add(
                new TargetedOcrRegion(
                    observation,
                    RasterCropGeometry.FromNormalized(
                        observation.Bounds,
                        pagePixelWidth,
                        pagePixelHeight)));
        }

        return result;
    }
}
