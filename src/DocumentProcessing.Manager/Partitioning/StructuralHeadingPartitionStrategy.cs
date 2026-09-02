namespace DocumentProcessing.Manager.Partitioning;

/// <summary>
/// Builds a complete fallback proposal from the shallowest unambiguous level
/// of deterministic structural-heading evidence.
/// </summary>
public sealed class StructuralHeadingPartitionStrategy
    : IDocumentPartitionStrategy
{
    /// <summary>Stable identifier for the first structural-heading policy.</summary>
    public const string StructuralHeadingStrategyId =
        "structural-heading-v1";

    /// <inheritdoc />
    public string StrategyId =>
        StructuralHeadingStrategyId;

    /// <inheritdoc />
    public DocumentPartitionProposal? TryPropose(
        DocumentPartitionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(
            evidence);

        return HierarchicalPartitionProposalBuilder.TryBuild(
            evidence,
            DocumentPartitionEvidenceOrigin.StructuralHeading,
            StrategyId,
            DocumentPartitionProposalReliability.Fallback,
            rejectDuplicateCoordinates:
                true,
            failClosedOnAmbiguousLevel:
                true);
    }
}
