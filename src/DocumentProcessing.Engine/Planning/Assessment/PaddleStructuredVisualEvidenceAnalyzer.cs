using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Planning;
using StbImageSharp;

namespace DocumentProcessing.Engine.Planning;

/// <summary>
/// Optional Paddle-backed analysis for a structured visual that deterministic
/// publication facts could not qualify.
/// </summary>
internal sealed class PaddleStructuredVisualEvidenceAnalyzer(
    IPageLayoutAnalyzer layoutAnalyzer)
{
    public async ValueTask<(VisualEvidenceKind EvidenceKind,
        bool WasPaddleInvoked)> AnalyzeAsync(
        MemoryStream visualContent,
        string mediaType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            visualContent);

        if (mediaType is not
            "image/jpeg" and not
            "image/png" and not
            "image/gif" and not
            "image/bmp")
        {
            return (
                VisualEvidenceKind.Unknown,
                false);
        }

        ImageInfo? imageInfo;

        try
        {
            imageInfo =
                ImageInfo.FromStream(
                    visualContent);
        }

        catch (OutOfMemoryException)
        {
            throw;
        }
        catch
        {
            return (
                VisualEvidenceKind.Unknown,
                false);
        }

        if (!imageInfo.HasValue)
        {
            return (
                VisualEvidenceKind.Unknown,
                false);
        }

        visualContent.Position =
            0;

        var layout =
            await layoutAnalyzer.AnalyzeAsync(
                    visualContent,
                    physicalPageNumber:
                        1,
                    imageInfo.Value.Width,
                    imageInfo.Value.Height,
                    cancellationToken)
                .ConfigureAwait(false);

        return (
            layout.Observations.Any(
                observation =>
                    observation.Kind is
                        LayoutObservationKind.Figure or
                        LayoutObservationKind.Table)
                ? VisualEvidenceKind.StructuredContentMeaningfulVisual
                : VisualEvidenceKind.Unknown,
            true);
    }
}
