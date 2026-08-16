using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// H.4D.4B.1 candidate portable projection.
///
/// CandidateDocument is the canonical deterministic text/layout/reconciliation
/// projection that can be represented honestly by the existing
/// DocumentIngestionResult model.
///
/// Source-preserved visuals and unresolved AnalyzeVisual evidence remain neutral
/// sidecars until H.4D.4B.2 provides the final persistence/disposition boundary.
/// </summary>
public sealed record DocumentControlledCandidatePortableOutput
{
    public DocumentControlledCandidatePortableOutput(
        DocumentIngestionResult candidateDocument,
        IEnumerable<DocumentControlledCandidateSourceVisualProvenance>
            sourceVisuals,
        IEnumerable<DocumentControlledCandidateVisualAnalysisProvenance>
            visualAnalyses)
    {
        CandidateDocument =
            candidateDocument ??
            throw new ArgumentNullException(
                nameof(candidateDocument));

        ArgumentNullException.ThrowIfNull(
            sourceVisuals);

        ArgumentNullException.ThrowIfNull(
            visualAnalyses);

        var materializedSourceVisuals =
            sourceVisuals.ToArray();

        var materializedAnalyses =
            visualAnalyses.ToArray();

        ValidateSidecars(
            CandidateDocument,
            materializedSourceVisuals,
            materializedAnalyses);

        SourceVisuals =
            Array.AsReadOnly(
                materializedSourceVisuals);

        VisualAnalyses =
            Array.AsReadOnly(
                materializedAnalyses);
    }

    public DocumentIngestionResult CandidateDocument { get; }

    public IReadOnlyList<DocumentControlledCandidateSourceVisualProvenance>
        SourceVisuals { get; }

    public IReadOnlyList<DocumentControlledCandidateVisualAnalysisProvenance>
        VisualAnalyses { get; }

    public bool HasUnpersistedSourceVisualAssets =>
        SourceVisuals.Count >
        0;

    public bool HasUnresolvedVisualAnalysis =>
        VisualAnalyses.Count >
        0;

    public bool IsCompleteForFinalCutoverComparison =>
        !HasUnpersistedSourceVisualAssets &&
        !HasUnresolvedVisualAnalysis;

    private static void ValidateSidecars(
        DocumentIngestionResult document,
        IReadOnlyList<DocumentControlledCandidateSourceVisualProvenance>
            sourceVisuals,
        IReadOnlyList<DocumentControlledCandidateVisualAnalysisProvenance>
            visualAnalyses)
    {
        var keys =
            new HashSet<(int PhysicalPageNumber, int SourceVisualIndex)>();

        foreach (var visual in
                 sourceVisuals)
        {
            if (!string.Equals(
                    visual.SourceDocumentSha256,
                    document.Source.Sha256,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Source visual provenance belongs to a different source document.",
                    nameof(sourceVisuals));
            }

            ValidatePhysicalPage(
                visual.PhysicalPageNumber,
                document.Source.PhysicalPageCount);

            if (!keys.Add(
                    (
                        visual.PhysicalPageNumber,
                        visual.SourceVisualIndex
                    )))
            {
                throw new ArgumentException(
                    "Candidate visual sidecars cannot duplicate a source occurrence.");
            }
        }

        foreach (var analysis in
                 visualAnalyses)
        {
            if (!string.Equals(
                    analysis.SourceDocumentSha256,
                    document.Source.Sha256,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Visual-analysis provenance belongs to a different source document.",
                    nameof(visualAnalyses));
            }

            ValidatePhysicalPage(
                analysis.PhysicalPageNumber,
                document.Source.PhysicalPageCount);

            if (!keys.Add(
                    (
                        analysis.PhysicalPageNumber,
                        analysis.SourceVisualIndex
                    )))
            {
                throw new ArgumentException(
                    "Candidate visual sidecars cannot duplicate a source occurrence.");
            }
        }
    }

    private static void ValidatePhysicalPage(
        int physicalPageNumber,
        int physicalPageCount)
    {
        if (physicalPageNumber <=
                0 ||
            physicalPageNumber >
                physicalPageCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Candidate visual sidecar page is outside source custody.");
        }
    }
}
