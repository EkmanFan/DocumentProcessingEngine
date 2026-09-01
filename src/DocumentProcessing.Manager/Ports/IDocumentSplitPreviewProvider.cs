using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Ports;

/// <summary>Provides lightweight physical-page metadata and PNG previews.</summary>
public interface IDocumentSplitPreviewProvider
{
    /// <summary>Inspects a pending whole-document unit.</summary>
    ValueTask<DocumentSplitPreviewManifest> InspectAsync(
        ProcessingUnitId unitId,
        CancellationToken cancellationToken = default);

    /// <summary>Renders one physical page as PNG bytes.</summary>
    ValueTask<byte[]> RenderPageAsync(
        ProcessingUnitId unitId,
        int physicalPageNumber,
        CancellationToken cancellationToken = default);
}

/// <summary>Describes one preview-capable pending document.</summary>
public sealed record DocumentSplitPreviewManifest(
    /// <summary>Gets the pending processing-unit identity.</summary>
    ProcessingUnitId UnitId,
    /// <summary>Gets the source submission identity.</summary>
    DocumentSubmissionId SubmissionId,
    /// <summary>Gets the original file name.</summary>
    string OriginalFileName,
    /// <summary>Gets the source physical page count.</summary>
    int PhysicalPageCount,
    /// <summary>Gets whether the configured complexity threshold recommends splitting.</summary>
    bool SplitSuggested);
