using DocumentProcessing.Core.Documents;
namespace DocumentProcessing.Core.Visual;

/// <summary>
/// User-provided callback through which the Engine writes one visual asset to
/// a consumer-owned destination.
/// </summary>
/// <remarks>
/// The callback currently opens the writable destination stream for a visual
/// identified by a format-neutral <see cref="UserVisualAssetWriteRequest"/>.
/// The Engine retains the preservation decision, byte transfer, validation
/// and custody evidence.
/// </remarks>
public delegate ValueTask<Stream>
    UserVisualAssetWriter(
        DocumentSource source,
        UserVisualAssetWriteRequest visual,
        CancellationToken cancellationToken);
