namespace DocumentProcessing.Manager.Partitioning;

/// <summary>Describes the evidence-backed strength of a partition proposal.</summary>
public enum DocumentPartitionProposalReliability
{
    /// <summary>Strong native or reconciled evidence supports the proposal.</summary>
    Strong = 0,

    /// <summary>The proposal is useful but requires explicit qualification.</summary>
    Qualified = 1,

    /// <summary>The proposal comes from an explicitly enabled fallback.</summary>
    Fallback = 2
}

/// <summary>Defines one inclusive source extent.</summary>
public sealed record DocumentPartitionExtent
{
    /// <summary>Gets the inclusive first source position.</summary>
    public DocumentPartitionPosition Start { get; }

    /// <summary>Gets the inclusive last source position.</summary>
    public DocumentPartitionPosition End { get; }

    /// <summary>Creates one inclusive source extent.</summary>
    public DocumentPartitionExtent(
        DocumentPartitionPosition start,
        DocumentPartitionPosition end)
    {
        ArgumentNullException.ThrowIfNull(
            start);

        ArgumentNullException.ThrowIfNull(
            end);

        if (start.GetType() !=
            end.GetType())
        {
            throw new ArgumentException(
                "Partition-extent positions must use the same coordinate kind.",
                nameof(end));
        }

        if (end.Coordinate <
            start.Coordinate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(end),
                "Partition-extent end cannot precede its start.");
        }

        Start =
            start;

        End =
            end;
    }
}

/// <summary>Describes one proposed atomic processing segment.</summary>
public sealed record DocumentPartitionSegment
{
    /// <summary>Gets the optional evidence-backed user-visible title.</summary>
    public string? SuggestedTitle { get; }

    /// <summary>Gets the inclusive source extent.</summary>
    public DocumentPartitionExtent Extent { get; }

    /// <summary>Creates one proposed processing segment.</summary>
    public DocumentPartitionSegment(
        string? suggestedTitle,
        DocumentPartitionExtent extent)
    {
        ArgumentNullException.ThrowIfNull(
            extent);

        SuggestedTitle =
            string.IsNullOrWhiteSpace(
                suggestedTitle)
                ? null
                : suggestedTitle.Trim();

        Extent =
            extent;
    }
}

/// <summary>
/// Contains a complete, ordered and non-destructive automatic partition plan.
/// </summary>
public sealed record DocumentPartitionProposal
{
    /// <summary>Gets the stable strategy identifier.</summary>
    public string StrategyId { get; }

    /// <summary>Gets the source coordinate axis.</summary>
    public DocumentPartitionAxis Axis { get; }

    /// <summary>Gets the categorical proposal reliability.</summary>
    public DocumentPartitionProposalReliability Reliability { get; }

    /// <summary>Gets every proposed segment in source order.</summary>
    public IReadOnlyList<DocumentPartitionSegment> Segments { get; }

    /// <summary>Creates one validated complete partition proposal.</summary>
    public DocumentPartitionProposal(
        string strategyId,
        DocumentPartitionAxis axis,
        DocumentPartitionProposalReliability reliability,
        IReadOnlyList<DocumentPartitionSegment> segments)
    {
        if (string.IsNullOrWhiteSpace(
                strategyId))
        {
            throw new ArgumentException(
                "Partition-strategy identifier cannot be empty.",
                nameof(strategyId));
        }

        ArgumentNullException.ThrowIfNull(
            axis);

        if (!Enum.IsDefined(
                reliability))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reliability),
                reliability,
                "Unknown partition-proposal reliability.");
        }

        ArgumentNullException.ThrowIfNull(
            segments);

        var segmentArray =
            segments.ToArray();

        if (segmentArray.Length <
            2 ||
            segmentArray.Any(
                segment =>
                    segment is null))
        {
            throw new ArgumentException(
                "Automatic partition proposals require at least two non-null segments.",
                nameof(segments));
        }

        ValidateCompleteCoverage(
            axis,
            segmentArray,
            nameof(segments));

        StrategyId =
            strategyId.Trim();

        Axis =
            axis;

        Reliability =
            reliability;

        Segments =
            segmentArray;
    }

    private static void ValidateCompleteCoverage(
        DocumentPartitionAxis axis,
        IReadOnlyList<DocumentPartitionSegment> segments,
        string parameterName)
    {
        if (segments.Any(
                segment =>
                    !axis.Contains(
                        segment.Extent.Start) ||
                    !axis.Contains(
                        segment.Extent.End)) ||
            segments[0].Extent.Start.Coordinate !=
                axis.FirstCoordinate ||
            segments[^1].Extent.End.Coordinate !=
                axis.LastCoordinate)
        {
            throw new ArgumentException(
                "Partition proposals must remain inside and cover the complete source axis.",
                parameterName);
        }

        for (var index = 1;
             index < segments.Count;
             index++)
        {
            if (segments[index].Extent.Start.Coordinate !=
                segments[index - 1].Extent.End.Coordinate +
                1)
            {
                throw new ArgumentException(
                    "Partition-proposal segments must be ordered, contiguous and non-overlapping.",
                    parameterName);
            }
        }
    }
}
