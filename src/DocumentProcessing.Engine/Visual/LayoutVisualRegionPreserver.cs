using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Core.Visual;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Raster;

namespace DocumentProcessing.Engine.Visual;

/// <summary>
/// Materializes and preserves one semantically authorized layout visual region.
///
/// The semantic gate is <see cref="LayoutVisualEvidence"/> plus the shared
/// deterministic visual-disposition policy. A raw layout Figure label is never
/// sufficient to authorize preservation.
///
/// The caller owns the document-scoped raster session and the destination
/// stream. This class neither opens a document nor chooses a storage backend.
/// </summary>
public sealed class LayoutVisualRegionPreserver
{
    private readonly VisualAssetPreserver _assetPreserver;

    public LayoutVisualRegionPreserver(
        VisualAssetPreserver? assetPreserver = null)
    {
        _assetPreserver =
            assetPreserver ??
            new VisualAssetPreserver();
    }

    public async ValueTask<PreservedVisualEvidence> PreserveAsync(
        LayoutVisualEvidence evidence,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster,
        string sourceDocumentSha256,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            evidence);
        ArgumentNullException.ThrowIfNull(
            rasterSession);
        ArgumentNullException.ThrowIfNull(
            pageRaster);
        ArgumentNullException.ThrowIfNull(
            destination);

        cancellationToken.ThrowIfCancellationRequested();

        var disposition =
            VisualEvidenceDispositionPolicy.Decide(
                evidence.Kind);

        if (disposition !=
            VisualDisposition.PreserveMeaningfulVisual)
        {
            throw new InvalidOperationException(
                $"Layout visual observation " +
                $"{evidence.Observation.ObservationSequence} has evidence " +
                $"'{evidence.Kind}' and disposition '{disposition}'; " +
                "regional preservation is not authorized.");
        }

        ValidatePageRaster(
            evidence,
            rasterSession,
            pageRaster);

        var crop =
            RasterCropGeometry.FromNormalized(
                evidence.Observation.Bounds,
                pageRaster.OutputPixelWidth,
                pageRaster.OutputPixelHeight);

        await using var cropBytes =
            new MemoryStream();

        var cropRaster =
            await rasterSession
                .RenderRegionAsync(
                    evidence.Observation.PhysicalPageNumber,
                    pageRaster.OutputPixelWidth,
                    pageRaster.OutputPixelHeight,
                    crop,
                    cropBytes,
                    cancellationToken)
                .ConfigureAwait(false);

        ValidateCropRaster(
            evidence,
            pageRaster,
            crop,
            cropRaster);

        Rewind(
            cropBytes);

        return await _assetPreserver
            .PreserveAsync(
                cropBytes,
                destination,
                sourceDocumentSha256,
                cropRaster.ProfileId,
                cropRaster.MediaType,
                evidence.Observation,
                crop,
                pageRaster.OutputPixelWidth,
                pageRaster.OutputPixelHeight,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ValidatePageRaster(
        LayoutVisualEvidence evidence,
        IDocumentRasterizationSession rasterSession,
        RasterRenderResult pageRaster)
    {
        if (!pageRaster.IsFullPage)
        {
            throw new ArgumentException(
                "Regional preservation requires the full-page raster that was used for layout analysis.",
                nameof(pageRaster));
        }

        if (pageRaster.PhysicalPageNumber !=
            evidence.Observation.PhysicalPageNumber)
        {
            throw new ArgumentException(
                "Layout visual evidence and page raster belong to different physical pages.",
                nameof(pageRaster));
        }

        if (!string.Equals(
                rasterSession.ProfileId,
                pageRaster.ProfileId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Raster session profile does not match the supplied page raster.");
        }
    }

    private static void ValidateCropRaster(
        LayoutVisualEvidence evidence,
        RasterRenderResult pageRaster,
        PixelRectangle expectedCrop,
        RasterRenderResult cropRaster)
    {
        if (cropRaster.PhysicalPageNumber !=
            evidence.Observation.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                "Regional raster belongs to a different physical page.");
        }

        if (cropRaster.Crop !=
            expectedCrop)
        {
            throw new InvalidDataException(
                "Regional raster does not match the deterministic layout crop.");
        }

        if (cropRaster.SourcePagePixelWidth !=
                pageRaster.OutputPixelWidth ||
            cropRaster.SourcePagePixelHeight !=
                pageRaster.OutputPixelHeight)
        {
            throw new InvalidDataException(
                "Regional raster source dimensions do not match the layout page raster.");
        }

        if (!string.Equals(
                cropRaster.ProfileId,
                pageRaster.ProfileId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Regional raster profile does not match the layout page raster.");
        }
    }

    private static void Rewind(
        Stream stream)
    {
        if (!stream.CanSeek)
        {
            throw new InvalidOperationException(
                "Internal regional visual buffer must be seekable.");
        }

        stream.Position =
            0;
    }
}
