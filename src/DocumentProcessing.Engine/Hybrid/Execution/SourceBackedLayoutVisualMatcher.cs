using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Planning;

namespace DocumentProcessing.Engine.Hybrid;

/// <summary>
/// Projects already-qualified source visuals into layout reading order.
///
/// The source visual remains the unit of preservation. Backend Figure regions
/// may describe or fragment it, but they never create an additional visual
/// asset without a corresponding source-visual plan.
/// </summary>
internal static class SourceBackedLayoutVisualMatcher
{
    private const string SyntheticRawLabelPrefix =
        "source_visual:";

    public static LayoutAnalysisResult AddSourceFigures(
        PageExecutionPlan candidatePlan,
        IReadOnlyList<VisualRasterObservation> sourceVisualObservations,
        LayoutAnalysisResult layout)
    {
        ArgumentNullException.ThrowIfNull(
            candidatePlan);

        ArgumentNullException.ThrowIfNull(
            sourceVisualObservations);

        ArgumentNullException.ThrowIfNull(
            layout);

        if (layout.Observations.Any(
                observation =>
                    observation.ReadingOrder is null))
        {
            return layout;
        }

        var sourceByIndex =
            sourceVisualObservations
                .ToDictionary(
                    visual =>
                        visual.SourceVisualIndex);

        var sources =
            candidatePlan.VisualElements
                .Where(
                    visual =>
                        visual.Action ==
                        VisualExecutionAction.PreserveMeaningfulVisual)
                .OrderBy(
                    visual =>
                        visual.SourceVisualIndex)
                .Select(
                    visual =>
                        sourceByIndex.TryGetValue(
                            visual.SourceVisualIndex,
                            out var source)
                            ? source
                            : null)
                .Where(
                    source =>
                        source is not null)
                .Cast<VisualRasterObservation>()
                .Where(
                    source =>
                        !layout.Observations.Any(
                            observation =>
                                string.Equals(
                                    observation.RawLabel,
                                    SyntheticRawLabelPrefix +
                                    source.SourceVisualIndex,
                                    StringComparison.Ordinal)))
                .ToArray();

        if (sources.Length ==
            0)
        {
            return layout;
        }

        var originals =
            layout.Observations
                .OrderBy(
                    observation =>
                        observation.ReadingOrder)
                .ThenBy(
                    observation =>
                        observation.ObservationSequence)
                .ToArray();

        var nextSequence =
            originals
                .Select(
                    observation =>
                        observation.ObservationSequence)
                .DefaultIfEmpty(-1)
                .Max() +
            1;

        var insertions =
            new List<SourceFigureInsertion>(
                sources.Length);

        foreach (var source in
                 sources)
        {
            var bounds =
                ResolveSourceBounds(
                    source);

            if (!TryResolveInsertionIndex(
                    originals,
                    bounds,
                    out var insertionIndex))
            {
                return layout;
            }

            insertions.Add(
                new SourceFigureInsertion(
                    insertionIndex,
                    source.SourceVisualIndex,
                    new LayoutObservation(
                        layout.PhysicalPageNumber,
                        nextSequence,
                        readingOrder:
                            0,
                        LayoutObservationKind.Figure,
                        bounds,
                        SyntheticRawLabelPrefix +
                        source.SourceVisualIndex)));

            nextSequence++;
        }

        var merged =
            new List<LayoutObservation>(
                originals.Length +
                insertions.Count);

        for (var index = 0;
             index <=
             originals.Length;
             index++)
        {
            merged.AddRange(
                insertions
                    .Where(
                        insertion =>
                            insertion.Index ==
                            index)
                    .OrderBy(
                        insertion =>
                            insertion.Observation.Bounds.Top)
                    .ThenBy(
                        insertion =>
                            insertion.Observation.Bounds.Left)
                    .ThenBy(
                        insertion =>
                            insertion.SourceVisualIndex)
                    .Select(
                        insertion =>
                            insertion.Observation));

            if (index <
                originals.Length)
            {
                merged.Add(
                    originals[index]);
            }
        }

        var reindexed =
            merged
                .Select(
                    (observation, readingOrder) =>
                        new LayoutObservation(
                            observation.PhysicalPageNumber,
                            observation.ObservationSequence,
                            readingOrder,
                            observation.Kind,
                            observation.Bounds,
                            observation.RawLabel))
                .ToArray();

        return new LayoutAnalysisResult(
            layout.BackendId,
            layout.PhysicalPageNumber,
            reindexed);
    }

    public static bool TryResolve(
        PageExecutionPlan candidatePlan,
        IReadOnlyList<VisualRasterObservation> sourceVisualObservations,
        LayoutAnalysisResult layout,
        out LayoutVisualEvidence[] resolved)
    {
        ArgumentNullException.ThrowIfNull(
            candidatePlan);

        ArgumentNullException.ThrowIfNull(
            sourceVisualObservations);

        ArgumentNullException.ThrowIfNull(
            layout);

        resolved =
            [];

        var preservingPlans =
            candidatePlan.VisualElements
                .Where(
                    visual =>
                        visual.Action ==
                        VisualExecutionAction.PreserveMeaningfulVisual)
                .OrderBy(
                    visual =>
                        visual.SourceVisualIndex)
                .ToArray();

        if (preservingPlans.Length ==
            0)
        {
            return false;
        }

        var sourceByIndex =
            sourceVisualObservations
                .ToDictionary(
                    visual =>
                        visual.SourceVisualIndex);

        var sourceBacked =
            new List<LayoutVisualEvidence>(
                preservingPlans.Length);

        foreach (var plan in
                 preservingPlans)
        {
            if (!sourceByIndex.TryGetValue(
                    plan.SourceVisualIndex,
                    out var source))
            {
                return false;
            }

            var rawLabel =
                SyntheticRawLabelPrefix +
                plan.SourceVisualIndex;

            var matches =
                layout.Observations
                    .Where(
                        observation =>
                            observation.Kind ==
                                LayoutObservationKind.Figure &&
                            string.Equals(
                                observation.RawLabel,
                                rawLabel,
                                StringComparison.Ordinal))
                    .ToArray();

            if (matches.Length !=
                    1 ||
                matches[0].ReadingOrder is null ||
                matches[0].Bounds !=
                    ResolveSourceBounds(
                        source))
            {
                return false;
            }

            sourceBacked.Add(
                new LayoutVisualEvidence(
                    matches[0],
                    VisualEvidenceKind.SourceBackedMeaningfulVisual));
        }

        resolved =
            sourceBacked
                .OrderBy(
                    evidence =>
                        evidence.Observation.ReadingOrder)
                .ThenBy(
                    evidence =>
                        evidence.Observation.ObservationSequence)
                .ToArray();

        return resolved.Length ==
               preservingPlans.Length;
    }

    private static bool TryResolveInsertionIndex(
        IReadOnlyList<LayoutObservation> originals,
        NormalizedRectangle bounds,
        out int insertionIndex)
    {
        var horizontallyComparable =
            originals
                .Select(
                    (observation, index) =>
                        new
                        {
                            Observation = observation,
                            Index = index
                        })
                .Where(
                    candidate =>
                        HorizontallyIntersects(
                            bounds,
                            candidate.Observation.Bounds))
                .ToArray();

        var lowerBound =
            horizontallyComparable
                .Where(
                    candidate =>
                        candidate.Observation.Bounds.Bottom <=
                        bounds.Top)
                .Select(
                    candidate =>
                        candidate.Index +
                        1)
                .DefaultIfEmpty(0)
                .Max();

        var upperBound =
            horizontallyComparable
                .Where(
                    candidate =>
                        candidate.Observation.Bounds.Top >=
                        bounds.Bottom)
                .Select(
                    candidate =>
                        candidate.Index)
                .DefaultIfEmpty(originals.Count)
                .Min();

        insertionIndex =
            lowerBound;

        return lowerBound <=
               upperBound;
    }

    private static bool HorizontallyIntersects(
        NormalizedRectangle first,
        NormalizedRectangle second) =>
        Math.Max(
            first.Left,
            second.Left) <
        Math.Min(
            first.Right,
            second.Right);

    private static NormalizedRectangle ResolveSourceBounds(
        VisualRasterObservation source) =>
        source.EffectiveVisualBounds ??
        source.DeclaredPageBounds;

    private sealed record SourceFigureInsertion(
        int Index,
        int SourceVisualIndex,
        LayoutObservation Observation);
}
