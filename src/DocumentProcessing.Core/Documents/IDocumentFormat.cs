namespace DocumentProcessing.Core.Documents;

/// <summary>
/// Neutral contract implemented by one concrete document format.
///
/// A format recognizes its own source representation and returns only native
/// evidence: direct source facts and deterministic format-derived measurements.
/// Document assessment and processing decisions belong to the Engine.
/// </summary>
public interface IDocumentFormat
{
    DocumentFormatId Format { get; }

    ValueTask<NativeEvidenceExtractionResult> TryExtractNativeEvidenceAsync(
        DocumentSource source,
        CancellationToken cancellationToken = default);
}
