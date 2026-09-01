namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Optional format capability for lightweight physical-page inspection and
/// preview rendering without running document processing.
/// </summary>
public interface IPhysicalPagePreviewDocumentFormat : IDocumentFormat
{
    /// <summary>Returns the physical page count when this format recognizes the source.</summary>
    ValueTask<int?> TryGetPhysicalPageCountAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default);

    /// <summary>Renders one physical page as a preview image.</summary>
    ValueTask RenderPhysicalPagePreviewAsync(
        DocumentSource source,
        int physicalPageNumber,
        Stream destination,
        CancellationToken cancellationToken = default);
}
