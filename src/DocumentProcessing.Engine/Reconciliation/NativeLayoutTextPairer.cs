using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Layout;

namespace DocumentProcessing.Engine.Reconciliation;

/// <summary>
/// Deterministically pairs native PDF word evidence with OCR-authorized layout
/// targets.
///
/// Pairing is target-centric:
///
///     layout target
///         -> 0..N per-source-block projections
///         -> one ComparableNativeTextEvidence
///
/// Multiple source blocks are legitimate provenance parts, not competing
/// candidates. The pairer fails closed only when one projected native word is
/// claimed by more than one OCR-authorized layout target.
///
/// No overlap threshold, fuzzy score, OCR text, model confidence, or authority
/// decision participates in pairing.
/// </summary>
public static class NativeLayoutTextPairer
{
    public static IReadOnlyList<NativeLayoutTextPairing> Pair(
        IReadOnlyList<DocumentTextBlock> sourceBlocks,
        IReadOnlyList<LayoutObservation> targetLayoutObservations)
    {
        ArgumentNullException.ThrowIfNull(
            sourceBlocks);

        ArgumentNullException.ThrowIfNull(
            targetLayoutObservations);

        if (sourceBlocks.Any(
                block =>
                    block is null))
        {
            throw new ArgumentException(
                "Source blocks cannot contain null values.",
                nameof(sourceBlocks));
        }

        if (targetLayoutObservations.Any(
                observation =>
                    observation is null))
        {
            throw new ArgumentException(
                "Target layout observations cannot contain null values.",
                nameof(targetLayoutObservations));
        }

        if (targetLayoutObservations.Count == 0)
        {
            return [];
        }

        var physicalPageNumber =
            targetLayoutObservations[0]
                .PhysicalPageNumber;

        if (targetLayoutObservations.Any(
                observation =>
                    observation.PhysicalPageNumber !=
                    physicalPageNumber))
        {
            throw new ArgumentException(
                "All target layout observations must belong to the same physical page.",
                nameof(targetLayoutObservations));
        }

        var duplicateObservationSequence =
            targetLayoutObservations
                .GroupBy(
                    observation =>
                        observation.ObservationSequence)
                .FirstOrDefault(
                    group =>
                        group.Count() > 1);

        if (duplicateObservationSequence is not null)
        {
            throw new ArgumentException(
                "Target layout observation sequences must be unique within the page.",
                nameof(targetLayoutObservations));
        }

        foreach (var target in targetLayoutObservations)
        {
            if (LayoutTreatmentPolicy.Decide(
                    target.Kind) !=
                LayoutTreatment.RecognizeText)
            {
                throw new InvalidOperationException(
                    $"Layout kind {target.Kind} is not authorized for native/text pairing.");
            }
        }

        var orderedBlocks =
            sourceBlocks
                .OrderBy(
                    block =>
                        block.ReadingOrder is null)
                .ThenBy(
                    block =>
                        block.ReadingOrder ??
                        int.MaxValue)
                .ThenBy(
                    block =>
                        block.SourceSequence)
                .ToArray();

        var orderedTargets =
            targetLayoutObservations
                .OrderBy(
                    observation =>
                        observation.ReadingOrder is null)
                .ThenBy(
                    observation =>
                        observation.ReadingOrder ??
                        int.MaxValue)
                .ThenBy(
                    observation =>
                        observation.ObservationSequence)
                .ToArray();

        var candidates =
            new PairingCandidate[
                orderedTargets.Length];

        for (var targetIndex = 0;
             targetIndex < orderedTargets.Length;
             targetIndex++)
        {
            var target =
                orderedTargets[targetIndex];

            var extents =
                new List<ComparableNativeTextExtent>();

            foreach (var block in orderedBlocks)
            {
                var extent =
                    NativeTextExtentProjector.Project(
                        block,
                        target);

                if (extent is not null)
                {
                    extents.Add(
                        extent);
                }
            }

            candidates[targetIndex] =
                new PairingCandidate(
                    target,
                    extents.Count == 0
                        ? null
                        : new ComparableNativeTextEvidence(
                            target,
                            extents));
        }

        var ownersByWord =
            new Dictionary<DocumentWord, List<int>>(
                ReferenceEqualityComparer.Instance);

        for (var candidateIndex = 0;
             candidateIndex < candidates.Length;
             candidateIndex++)
        {
            var evidence =
                candidates[candidateIndex]
                    .Evidence;

            if (evidence is null)
            {
                continue;
            }

            foreach (var word in evidence.Words)
            {
                if (!ownersByWord.TryGetValue(
                        word,
                        out var owners))
                {
                    owners =
                        [];

                    ownersByWord.Add(
                        word,
                        owners);
                }

                owners.Add(
                    candidateIndex);
            }
        }

        var ambiguousWordsByCandidate =
            new Dictionary<int, List<DocumentWord>>();

        foreach (var ownership in ownersByWord)
        {
            if (ownership.Value.Count <= 1)
            {
                continue;
            }

            foreach (var candidateIndex in ownership.Value)
            {
                if (!ambiguousWordsByCandidate.TryGetValue(
                        candidateIndex,
                        out var ambiguousWords))
                {
                    ambiguousWords =
                        [];

                    ambiguousWordsByCandidate.Add(
                        candidateIndex,
                        ambiguousWords);
                }

                ambiguousWords.Add(
                    ownership.Key);
            }
        }

        var results =
            new NativeLayoutTextPairing[
                candidates.Length];

        for (var candidateIndex = 0;
             candidateIndex < candidates.Length;
             candidateIndex++)
        {
            var candidate =
                candidates[candidateIndex];

            if (ambiguousWordsByCandidate.TryGetValue(
                    candidateIndex,
                    out var ambiguousWords))
            {
                results[candidateIndex] =
                    new NativeLayoutTextPairing(
                        candidate.Target,
                        NativeLayoutTextPairingStatus
                            .AmbiguousWordOwnership,
                        comparableNativeEvidence: null,
                        ambiguousWords
                            .OrderBy(
                                word =>
                                    word.SourceSequence)
                            .ToArray());

                continue;
            }

            if (candidate.Evidence is null)
            {
                results[candidateIndex] =
                    new NativeLayoutTextPairing(
                        candidate.Target,
                        NativeLayoutTextPairingStatus
                            .NoNativeEvidence);

                continue;
            }

            results[candidateIndex] =
                new NativeLayoutTextPairing(
                    candidate.Target,
                    NativeLayoutTextPairingStatus
                        .Comparable,
                    candidate.Evidence);
        }

        return results;
    }

    private sealed record PairingCandidate(
        LayoutObservation Target,
        ComparableNativeTextEvidence? Evidence);
}
