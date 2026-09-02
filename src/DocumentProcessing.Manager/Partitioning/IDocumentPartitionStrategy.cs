namespace DocumentProcessing.Manager.Partitioning;

/// <summary>
/// Converts already acquired neutral structure evidence into a deterministic
/// partition proposal without performing I/O or mutating Manager state.
/// </summary>
public interface IDocumentPartitionStrategy
{
    /// <summary>Gets the stable strategy identifier used for audit and tests.</summary>
    string StrategyId { get; }

    /// <summary>
    /// Returns a complete proposal, or <see langword="null"/> when this strategy
    /// cannot safely conclude one from the supplied evidence.
    /// </summary>
    DocumentPartitionProposal? TryPropose(
        DocumentPartitionEvidence evidence);
}
