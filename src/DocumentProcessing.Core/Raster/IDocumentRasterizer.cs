using DocumentProcessing.Core.Documents;

namespace DocumentProcessing.Core.Raster;

/// <summary>
/// Format capability boundary for opening one document-scoped rasterization
/// session.
///
/// The session boundary is intentional: page and region rendering must reuse one
/// materialized source rather than copying a potentially large document for
/// every crop.
/// </summary>
public interface IDocumentRasterizer
{
    bool CanRasterize(
        DocumentFormatId format);

    ValueTask<IDocumentRasterizationSession> OpenAsync(
        DocumentSource source,
        DocumentFormatId format,
        CancellationToken cancellationToken = default);
}
