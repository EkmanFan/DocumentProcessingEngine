namespace DocumentProcessing.Core.Raster;

/// <summary>
/// Document-scoped raster execution capability.
///
/// Implementations own their internal source/materialization lifetime. Output
/// bytes are written to caller-owned destination streams.
/// </summary>
public interface IDocumentRasterizationSession
    : IAsyncDisposable
{
    string BackendId { get; }

    string ProfileId { get; }

    int Dpi { get; }

    ValueTask<RasterRenderResult> RenderPageAsync(
        int physicalPageNumber,
        Stream destination,
        CancellationToken cancellationToken = default);

    ValueTask<RasterRenderResult> RenderRegionAsync(
        int physicalPageNumber,
        int sourcePagePixelWidth,
        int sourcePagePixelHeight,
        PixelRectangle crop,
        Stream destination,
        CancellationToken cancellationToken = default);
}
