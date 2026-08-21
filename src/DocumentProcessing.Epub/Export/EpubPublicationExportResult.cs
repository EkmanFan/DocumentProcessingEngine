namespace DocumentProcessing.Epub.Export;

/// <summary>
/// Summary of one completed EPUB publication export.
/// </summary>
public sealed record EpubPublicationExportResult(
    string Identifier,
    int ContentDocumentCount,
    int VisualAssetCount,
    int OmittedElementCount);
