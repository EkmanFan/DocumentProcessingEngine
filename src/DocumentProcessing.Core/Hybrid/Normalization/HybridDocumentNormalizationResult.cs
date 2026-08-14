namespace DocumentProcessing.Core.Hybrid.Normalization;

/// <summary>
/// Unified normalized hybrid stream, still before structural segmentation.
///
/// Every source page and source element remains reachable by reference through
/// the normalized projection.
/// </summary>
public sealed class HybridDocumentNormalizationResult
{
    public HybridDocumentNormalizationResult(
        HybridDocumentAssemblyResult sourceAssembly,
        string normalizationProfileId,
        IReadOnlyList<NormalizedHybridDocumentPage>? pages = null)
    {
        SourceAssembly =
            sourceAssembly ??
            throw new ArgumentNullException(
                nameof(sourceAssembly));

        if (string.IsNullOrWhiteSpace(
                normalizationProfileId))
        {
            throw new ArgumentException(
                "Normalization profile identifier cannot be empty.",
                nameof(normalizationProfileId));
        }

        var resolved =
            pages ??
            Array.Empty<NormalizedHybridDocumentPage>();

        if (resolved.Count !=
            SourceAssembly.Pages.Count)
        {
            throw new ArgumentException(
                "Hybrid normalization must preserve every source page exactly once.",
                nameof(pages));
        }

        for (var index = 0;
             index < resolved.Count;
             index++)
        {
            if (!ReferenceEquals(
                    resolved[index].SourcePage,
                    SourceAssembly.Pages[index]))
            {
                throw new ArgumentException(
                    "Hybrid normalization must preserve source-page identity and order.",
                    nameof(pages));
            }
        }

        NormalizationProfileId =
            normalizationProfileId.Trim();

        Pages =
            resolved.ToArray();
    }

    public HybridDocumentAssemblyResult SourceAssembly { get; }

    public string NormalizationProfileId { get; }

    public IReadOnlyList<NormalizedHybridDocumentPage> Pages { get; }

    public bool HasUnresolvedEvidence =>
        Pages.Any(
            page =>
                page.HasUnresolvedEvidence);
}
