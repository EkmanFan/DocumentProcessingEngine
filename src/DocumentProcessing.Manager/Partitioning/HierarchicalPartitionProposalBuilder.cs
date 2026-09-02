namespace DocumentProcessing.Manager.Partitioning;

/// <summary>
/// Builds complete proposals from one hierarchy level of already validated
/// neutral boundary evidence.
/// </summary>
internal static class HierarchicalPartitionProposalBuilder
{
    public static DocumentPartitionProposal? TryBuild(
        DocumentPartitionEvidence evidence,
        DocumentPartitionEvidenceOrigin origin,
        string strategyId,
        DocumentPartitionProposalReliability reliability,
        bool rejectDuplicateCoordinates,
        bool failClosedOnAmbiguousLevel)
    {
        foreach (var candidateLevel in
                 evidence.Boundaries
                     .Where(
                         boundary =>
                             boundary.Origin ==
                             origin)
                     .GroupBy(
                         boundary =>
                             boundary.HierarchyLevel)
                     .OrderBy(
                         level =>
                             level.Key))
        {
            var ordered =
                candidateLevel
                    .OrderBy(
                        boundary =>
                            boundary.SourceOrder)
                    .ToArray();

            if (rejectDuplicateCoordinates &&
                ordered
                    .Select(
                        boundary =>
                            boundary.Position.Coordinate)
                    .Distinct()
                    .Count() !=
                ordered.Length)
            {
                if (failClosedOnAmbiguousLevel)
                {
                    return null;
                }

                continue;
            }

            var boundaries =
                ordered
                    .GroupBy(
                        boundary =>
                            boundary.Position.Coordinate)
                    .Select(
                        position =>
                            position.First())
                    .ToArray();

            if (boundaries.Length <
                2)
            {
                continue;
            }

            if (!CoordinatesIncreaseStrictly(
                    boundaries))
            {
                if (failClosedOnAmbiguousLevel)
                {
                    return null;
                }

                continue;
            }

            return BuildProposal(
                evidence.Axis,
                boundaries,
                strategyId,
                reliability);
        }

        return null;
    }

    private static DocumentPartitionProposal BuildProposal(
        DocumentPartitionAxis axis,
        IReadOnlyList<DocumentPartitionBoundary> boundaries,
        string strategyId,
        DocumentPartitionProposalReliability reliability)
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
            strategyId,
            axis,
            reliability,
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
