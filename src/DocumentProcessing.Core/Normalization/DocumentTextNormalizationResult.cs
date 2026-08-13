using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.Core.Normalization;

public sealed class DocumentTextNormalizationResult
{
    public DocumentTextNormalizationResult(
        DocumentExtractionResult sourceExtraction,
        string normalizationProfileId,
        IReadOnlyList<NormalizedDocumentPage>? pages = null)
    {
        SourceExtraction =
            sourceExtraction ??
            throw new ArgumentNullException(nameof(sourceExtraction));

        if (string.IsNullOrWhiteSpace(normalizationProfileId))
        {
            throw new ArgumentException(
                "Normalization profile identifier cannot be empty.",
                nameof(normalizationProfileId));
        }

        NormalizationProfileId =
            normalizationProfileId.Trim();

        Pages =
            pages ??
            Array.Empty<NormalizedDocumentPage>();
    }

    public DocumentExtractionResult SourceExtraction { get; }

    public DocumentFormatId Format =>
        SourceExtraction.Format;

    public string NormalizationProfileId { get; }

    public IReadOnlyList<NormalizedDocumentPage> Pages { get; }
}
