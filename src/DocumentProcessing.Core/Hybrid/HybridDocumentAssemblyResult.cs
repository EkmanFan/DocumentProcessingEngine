namespace DocumentProcessing.Core.Hybrid;

/// <summary>
/// Neutral pre-segmentation result of assembling native, OCR, unresolved, and
/// preserved visual evidence into one deterministic document stream.
///
/// This is intentionally not yet PagedDocumentProcessingModel. Structural
/// normalization/segmentation of the unified stream remains Phase 18 work.
/// </summary>
public sealed class HybridDocumentAssemblyResult
{
    public HybridDocumentAssemblyResult(
        string assemblyProfileId,
        IReadOnlyList<HybridDocumentPage>? pages = null)
    {
        if (string.IsNullOrWhiteSpace(
                assemblyProfileId))
        {
            throw new ArgumentException(
                "Assembly profile identifier cannot be empty.",
                nameof(assemblyProfileId));
        }

        var resolved =
            pages ??
            Array.Empty<HybridDocumentPage>();

        for (var index = 1;
             index < resolved.Count;
             index++)
        {
            if (resolved[index - 1].PhysicalPageNumber >=
                resolved[index].PhysicalPageNumber)
            {
                throw new ArgumentException(
                    "Hybrid document pages must be in strictly increasing physical-page order.",
                    nameof(pages));
            }
        }

        AssemblyProfileId =
            assemblyProfileId.Trim();

        Pages =
            resolved.ToArray();
    }

    public string AssemblyProfileId { get; }

    public IReadOnlyList<HybridDocumentPage> Pages { get; }

    public bool HasUnresolvedEvidence =>
        Pages.Any(
            page =>
                page.HasUnresolvedEvidence);
}
