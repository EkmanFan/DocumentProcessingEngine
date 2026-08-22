using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Core.Results;

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

    private const string SyntheticUnqualifiedRawLabelPrefix =
        "source_visual_unqualified:";

    private const double MinimumStrongOverlapRatio =
        0.5;

    public static bool TryResolveWithSourceFigures(
        PageExecutionPlan candidatePlan,
        IReadOnlyList<VisualRasterObservation> sourceVisualObservations,
        LayoutAnalysisResult layout,
        out LayoutAnalysisResult executionLayout,
        out LayoutVisualEvidence[] resolved)
    {
        executionLayout =
            AddSourceFigures(
                candidatePlan,
                sourceVisualObservations,
                layout);

        return TryResolve(
            candidatePlan,
            sourceVisualObservations,
            executionLayout,
            out resolved);
    }

    public static bool IsBackendFigureCoveredBySourceVisual(
        LayoutObservation observation,
        IReadOnlyList<LayoutVisualEvidence> sourceBacked)
    {
        ArgumentNullException.ThrowIfNull(
            observation);

        ArgumentNullException.ThrowIfNull(
            sourceBacked);

        return observation.Kind ==
                   LayoutObservationKind.Figure &&
               observation.RawLabel?.StartsWith(
                   SyntheticRawLabelPrefix,
                   StringComparison.Ordinal) !=
                   true &&
               observation.RawLabel?.StartsWith(
                   SyntheticUnqualifiedRawLabelPrefix,
                   StringComparison.Ordinal) !=
                   true &&
               sourceBacked.Any(
                   evidence =>
                       SmallerAreaOverlapRatio(
                           observation.Bounds,
                           evidence.Observation.Bounds) >=
                       MinimumStrongOverlapRatio);
    }

    public static DocumentVisualQualification GetQualification(
        LayoutObservation observation)
    {
        ArgumentNullException.ThrowIfNull(
            observation);

        return observation.RawLabel?.StartsWith(
                   SyntheticUnqualifiedRawLabelPrefix,
                   StringComparison.Ordinal) ==
               true
            ? DocumentVisualQualification.Unqualified
            : DocumentVisualQualification.Meaningful;
    }

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
                        IsSourcePreservationCandidate(
                            visual.Action))
                .OrderBy(
                    visual =>
                        visual.SourceVisualIndex)
                .Select(
                    visual =>
                        sourceByIndex.TryGetValue(
                            visual.SourceVisualIndex,
                            out var source)
                            ? new PlannedSourceVisual(
                                visual,
                                source)
                            : null)
                .Where(
                    source =>
                        source is not null)
                .Cast<PlannedSourceVisual>()
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
            var evidenceKind =
                ResolveSourceBackedEvidenceKind(
                    candidatePlan.PhysicalPageNumber,
                    source.Plan.Action,
                    source.Observation,
                    layout);

            if (evidenceKind ==
                VisualEvidenceKind.PublicationPresentationVisual)
            {
                continue;
            }

            var rawLabel =
                CreateSyntheticRawLabel(
                    evidenceKind,
                    source.Observation.SourceVisualIndex);

            if (layout.Observations.Any(
                    observation =>
                        string.Equals(
                            observation.RawLabel,
                            rawLabel,
                            StringComparison.Ordinal)))
            {
                continue;
            }

            var bounds =
                ResolveSourceBounds(
                    source.Observation);

            int insertionIndex;

            if (evidenceKind ==
                VisualEvidenceKind.SourceBackedUnqualifiedVisual)
            {
                insertionIndex =
                    originals.Length;
            }
            else if (!TryResolveInsertionIndex(
                         originals,
                         bounds,
                         out insertionIndex))
            {
                return layout;
            }

            insertions.Add(
                new SourceFigureInsertion(
                    insertionIndex,
                    source.Observation.SourceVisualIndex,
                    new LayoutObservation(
                        layout.PhysicalPageNumber,
                        nextSequence,
                        readingOrder:
                            0,
                        LayoutObservationKind.Figure,
                        bounds,
                        rawLabel)));

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
                        IsSourcePreservationCandidate(
                            visual.Action))
                .OrderBy(
                    visual =>
                        visual.SourceVisualIndex)
                .ToArray();

        if (preservingPlans.Length ==
            0)
        {
            return true;
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

            var evidenceKind =
                ResolveSourceBackedEvidenceKind(
                    candidatePlan.PhysicalPageNumber,
                    plan.Action,
                    source,
                    layout);

            if (evidenceKind ==
                VisualEvidenceKind.PublicationPresentationVisual)
            {
                continue;
            }

            var rawLabel =
                CreateSyntheticRawLabel(
                    evidenceKind,
                    plan.SourceVisualIndex);

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
                    evidenceKind));
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

        return true;
    }

    private static bool IsSourcePreservationCandidate(
        VisualExecutionAction action) =>
        action is
            VisualExecutionAction.PreserveMeaningfulVisual or
            VisualExecutionAction.PreserveUnqualifiedVisual or
            VisualExecutionAction.AnalyzeVisual;

    private static VisualEvidenceKind ResolveSourceBackedEvidenceKind(
        int physicalPageNumber,
        VisualExecutionAction action,
        VisualRasterObservation source,
        LayoutAnalysisResult layout) =>
        action switch
        {
            VisualExecutionAction.PreserveMeaningfulVisual =>
                VisualEvidenceKind.SourceBackedMeaningfulVisual,

            VisualExecutionAction.PreserveUnqualifiedVisual =>
                VisualEvidenceKind.SourceBackedUnqualifiedVisual,

            VisualExecutionAction.AnalyzeVisual
                when IsPublicationPresentationVisual(
                    physicalPageNumber,
                    source,
                    layout) =>
                VisualEvidenceKind.PublicationPresentationVisual,

            VisualExecutionAction.AnalyzeVisual
                when HasStrongMeaningfulLayoutEvidence(
                    source,
                    layout) =>
                VisualEvidenceKind.SourceBackedMeaningfulVisual,

            VisualExecutionAction.AnalyzeVisual =>
                VisualEvidenceKind.SourceBackedUnqualifiedVisual,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(action),
                    action,
                    "Unsupported source-visual preservation action.")
        };

    private static bool IsPublicationPresentationVisual(
        int physicalPageNumber,
        VisualRasterObservation source,
        LayoutAnalysisResult layout)
    {
        if (physicalPageNumber !=
            1)
        {
            return false;
        }

        var bounds =
            ResolveSourceBounds(
                source);

        var coversPage =
            bounds.Left <=
                0.01 &&
            bounds.Top <=
                0.01 &&
            bounds.Right >=
                0.99 &&
            bounds.Bottom >=
                0.99;

        return coversPage &&
               layout.Observations.Any(
                   observation =>
                       string.Equals(
                           observation.RawLabel,
                           "doc_title",
                           StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasStrongMeaningfulLayoutEvidence(
        VisualRasterObservation source,
        LayoutAnalysisResult layout)
    {
        var sourceBounds =
            ResolveSourceBounds(
                source);

        return layout.Observations.Any(
            observation =>
                (
                    observation.Kind ==
                        LayoutObservationKind.Table ||
                    string.Equals(
                        observation.RawLabel,
                        "formula",
                        StringComparison.OrdinalIgnoreCase)
                ) &&
                SmallerAreaOverlapRatio(
                    sourceBounds,
                    observation.Bounds) >=
                MinimumStrongOverlapRatio);
    }

    private static double SmallerAreaOverlapRatio(
        NormalizedRectangle first,
        NormalizedRectangle second)
    {
        var intersectionWidth =
            Math.Max(
                0,
                Math.Min(
                    first.Right,
                    second.Right) -
                Math.Max(
                    first.Left,
                    second.Left));

        var intersectionHeight =
            Math.Max(
                0,
                Math.Min(
                    first.Bottom,
                    second.Bottom) -
                Math.Max(
                    first.Top,
                    second.Top));

        var smallerArea =
            Math.Min(
                (first.Right - first.Left) *
                (first.Bottom - first.Top),
                (second.Right - second.Left) *
                (second.Bottom - second.Top));

        return smallerArea <=
                0
            ? 0
            : intersectionWidth *
              intersectionHeight /
              smallerArea;
    }

    private static string CreateSyntheticRawLabel(
        VisualEvidenceKind evidenceKind,
        int sourceVisualIndex) =>
        evidenceKind switch
        {
            VisualEvidenceKind.SourceBackedMeaningfulVisual =>
                SyntheticRawLabelPrefix +
                sourceVisualIndex,

            VisualEvidenceKind.SourceBackedUnqualifiedVisual =>
                SyntheticUnqualifiedRawLabelPrefix +
                sourceVisualIndex,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(evidenceKind),
                    evidenceKind,
                    "Synthetic source Figure requires preserved source evidence.")
        };

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

    private sealed record PlannedSourceVisual(
        VisualElementExecutionPlan Plan,
        VisualRasterObservation Observation);
}
