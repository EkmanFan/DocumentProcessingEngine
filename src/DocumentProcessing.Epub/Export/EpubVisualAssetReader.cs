using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Epub.Export;

/// <summary>
/// Opens the caller-owned bytes for one visual retained by the Engine result.
/// </summary>
/// <remarks>
/// The exporter disposes the returned readable stream after copying and
/// verifying it.
/// </remarks>
public delegate ValueTask<Stream> EpubVisualAssetReader(
    DocumentElement element,
    DocumentVisualAsset visualAsset,
    CancellationToken cancellationToken);
