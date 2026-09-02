namespace DocumentProcessing.Manager.Partitioning;

/// <summary>
/// Builds a complete proposal from the shallowest usable level of native
/// hierarchical navigation evidence.
/// </summary>
public sealed class NativeNavigationPartitionStrategy
    : IDocumentPartitionStrategy
{
    /// <summary>Stable identifier for the first native-navigation policy.</summary>
    public const string NativeNavigationStrategyId =
        "native-navigation-v1";

    /// <inheritdoc />
    public string StrategyId =>
        NativeNavigationStrategyId;

    /// <inheritdoc />
    public DocumentPartitionProposal? TryPropose(
        DocumentPartitionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(
            evidence);

        return HierarchicalPartitionProposalBuilder.TryBuild(
            evidence,
            DocumentPartitionEvidenceOrigin.NativeNavigation,
            StrategyId,
            DocumentPartitionProposalReliability.Qualified,
            rejectDuplicateCoordinates:
                false,
            failClosedOnAmbiguousLevel:
                false);
    }
}
