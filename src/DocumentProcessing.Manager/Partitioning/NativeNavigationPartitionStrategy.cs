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

        var candidateLevels =
            evidence.Boundaries
                .Where(
                    boundary =>
                        boundary.Origin ==
                        DocumentPartitionEvidenceOrigin.NativeNavigation)
                .GroupBy(
                    boundary =>
                        boundary.HierarchyLevel)
                .OrderBy(
                    level =>
                        level.Key);

        foreach (var candidateLevel in
                 candidateLevels)
        {
            var boundaries =
                candidateLevel
                    .OrderBy(
                        boundary =>
                            boundary.SourceOrder)
                    .GroupBy(
                        boundary =>
                            boundary.Position.Coordinate)
                    .Select(
                        position =>
                            position.First())
                    .ToArray();

            if (boundaries.Length <
                    2 ||
                !CoordinatesIncreaseStrictly(
                    boundaries))
            {
                continue;
            }

            return BuildProposal(
                evidence.Axis,
                boundaries);
        }

        return null;
    }

    private DocumentPartitionProposal BuildProposal(
        DocumentPartitionAxis axis,
        IReadOnlyList<DocumentPartitionBoundary> boundaries)
    {
        var starts =
            new List<SegmentStart>(
                boundaries.Count +
                1);

        if (boundaries[0].Position.Coordinate >
            axis.FirstCoordinate)
        {
            starts.Add(
                new SegmentStart(
                    axis.CreatePosition(
                        axis.FirstCoordinate),
                    SuggestedTitle:
                        null));
        }

        starts.AddRange(
            boundaries.Select(
                boundary =>
                    new SegmentStart(
                        boundary.Position,
                        boundary.Title)));

        var segments =
            new DocumentPartitionSegment[starts.Count];

        for (var index = 0;
             index < starts.Count;
             index++)
        {
            var endCoordinate =
                index ==
                starts.Count -
                1
                    ? axis.LastCoordinate
                    : starts[index + 1]
                        .Position
                        .Coordinate -
                      1;

            segments[index] =
                new DocumentPartitionSegment(
                    starts[index].SuggestedTitle,
                    new DocumentPartitionExtent(
                        starts[index].Position,
                        axis.CreatePosition(
                            endCoordinate)));
        }

        return new DocumentPartitionProposal(
            StrategyId,
            axis,
            DocumentPartitionProposalReliability.Qualified,
            segments);
    }

    private static bool CoordinatesIncreaseStrictly(
        IReadOnlyList<DocumentPartitionBoundary> boundaries)
    {
        for (var index = 1;
             index < boundaries.Count;
             index++)
        {
            if (boundaries[index].Position.Coordinate <=
                boundaries[index - 1].Position.Coordinate)
            {
                return false;
            }
        }

        return true;
    }

    private sealed record SegmentStart(
        DocumentPartitionPosition Position,
        string? SuggestedTitle);
}
