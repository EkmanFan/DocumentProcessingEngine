using DocumentProcessing.Core.Hybrid;

namespace DocumentProcessing.Engine.Hybrid;

/// <summary>
/// Produces a single deterministic page/document stream from already-validated
/// hybrid elements.
///
/// The assembler does not decide which text source is authoritative. It rejects
/// ambiguous ordering and duplicate provenance rather than silently merging or
/// reordering evidence.
/// </summary>
public static class HybridDocumentAssembler
{
    public const string AssemblyProfileId =
        "hybrid-evidence-assembly-v1";

    public static HybridDocumentPage AssemblePage(
        int physicalPageNumber,
        IEnumerable<HybridDocumentElement> elements)
    {
        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        ArgumentNullException.ThrowIfNull(
            elements);

        var resolved =
            elements.ToArray();

        if (resolved.Any(
                element =>
                    element.PhysicalPageNumber !=
                    physicalPageNumber))
        {
            throw new ArgumentException(
                "Every hybrid element must belong to the requested page.",
                nameof(elements));
        }

        RejectDuplicateReadingOrder(
            resolved);

        RejectDuplicateLayoutEvidence(
            resolved);

        RejectUnsafeNativeDuplication(
            resolved);

        var ordered =
            resolved
                .OrderBy(
                    element =>
                        element.ReadingOrder)
                .ToArray();

        return new HybridDocumentPage(
            physicalPageNumber,
            ordered);
    }

    public static HybridDocumentAssemblyResult AssembleDocument(
        IEnumerable<HybridDocumentPage> pages)
    {
        ArgumentNullException.ThrowIfNull(
            pages);

        var resolved =
            pages.ToArray();

        var duplicatePage =
            resolved
                .GroupBy(
                    page =>
                        page.PhysicalPageNumber)
                .FirstOrDefault(
                    group =>
                        group.Count() > 1);

        if (duplicatePage is not null)
        {
            throw new ArgumentException(
                $"Physical page {duplicatePage.Key} appears more than once.",
                nameof(pages));
        }

        return new HybridDocumentAssemblyResult(
            AssemblyProfileId,
            resolved
                .OrderBy(
                    page =>
                        page.PhysicalPageNumber)
                .ToArray());
    }

    private static void RejectDuplicateReadingOrder(
        IReadOnlyList<HybridDocumentElement> elements)
    {
        var duplicate =
            elements
                .GroupBy(
                    element =>
                        element.ReadingOrder)
                .FirstOrDefault(
                    group =>
                        group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Hybrid page has ambiguous reading order {duplicate.Key}.");
        }
    }

    private static void RejectDuplicateLayoutEvidence(
        IReadOnlyList<HybridDocumentElement> elements)
    {
        var duplicate =
            elements
                .Where(
                    element =>
                        element.LayoutObservation is not null)
                .GroupBy(
                    element =>
                        element.LayoutObservation!
                            .ObservationSequence)
                .FirstOrDefault(
                    group =>
                        group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Layout observation {duplicate.Key} was emitted more than once into the hybrid stream.");
        }
    }

    private static void RejectUnsafeNativeDuplication(
        IReadOnlyList<HybridDocumentElement> elements)
    {
        var groups =
            elements
                .Where(
                    element =>
                        element.NativeBlock is not null)
                .GroupBy(
                    element =>
                        element.NativeBlock!
                            .SourceSequence);

        foreach (var group in groups)
        {
            var candidates =
                group.ToArray();

            if (candidates.Length <= 1)
            {
                continue;
            }

            if (candidates.Any(
                    candidate =>
                        candidate.Reconciliation is null))
            {
                throw new InvalidOperationException(
                    $"Native block {group.Key} cannot appear both as standalone text and as reconciled text.");
            }

            var extents =
                candidates
                    .Select(
                        candidate =>
                            candidate.Reconciliation!
                                .ComparableNativeExtent)
                    .ToArray();

            if (extents.Any(
                    extent =>
                        extent is null))
            {
                throw new InvalidOperationException(
                    $"Native block {group.Key} participates in multiple reconciliations without explicit comparable extents.");
            }

            for (var leftIndex = 0;
                 leftIndex < extents.Length;
                 leftIndex++)
            {
                for (var rightIndex = leftIndex + 1;
                     rightIndex < extents.Length;
                     rightIndex++)
                {
                    var left =
                        extents[leftIndex]!;

                    var right =
                        extents[rightIndex]!;

                    if (RangesOverlap(
                            left.FirstWordIndex,
                            left.LastWordIndex,
                            right.FirstWordIndex,
                            right.LastWordIndex))
                    {
                        throw new InvalidOperationException(
                            $"Native block {group.Key} has overlapping comparable extents in the hybrid stream.");
                    }
                }
            }
        }
    }

    private static bool RangesOverlap(
        int firstLeft,
        int lastLeft,
        int firstRight,
        int lastRight) =>
        Math.Max(
            firstLeft,
            firstRight) <=
        Math.Min(
            lastLeft,
            lastRight);
}
