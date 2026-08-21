using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.Layout;
using DocumentProcessing.Engine.Reconciliation;

namespace DocumentProcessing.Engine.Hybrid;

/// <summary>
/// Merges authoritative native text blocks with already-preserved,
/// layout-backed semantic visuals.
///
/// This component performs no rasterization, layout analysis, OCR, visual
/// classification, or visual preservation. It only resolves one page-local
/// ordering seam after those upstream decisions have already been made.
///
/// Native text remains authoritative and its source-block provenance is retained
/// unchanged. Layout evidence is used only to place preserved visuals relative
/// to whole native blocks.
///
/// The merge fails closed when deterministic whole-block placement is not
/// possible:
/// - ambiguous native-word ownership;
/// - a native block with neither comparable layout-text ownership nor an
///   unambiguous geometry-derived band around preserved visuals;
/// - a native block whose layout-text ownership straddles a preserved visual;
/// - a text target sharing a reading-order value with a preserved visual;
/// - missing, duplicate, foreign, or inconsistent visual layout evidence.
///
/// It never splits native blocks. A future block-splitting capability, if ever
/// required by real corpus evidence, must be introduced explicitly rather than
/// inferred here.
/// </summary>
public static class NativeLayoutVisualPageAssembler
{
    #region Methods

    public static HybridDocumentPage Assemble(
        DocumentExtractionPage sourcePage,
        LayoutAnalysisResult layout,
        IReadOnlyList<HybridDocumentElement> visualElements)
    {
        ArgumentNullException.ThrowIfNull(
            sourcePage);

        ArgumentNullException.ThrowIfNull(
            layout);

        ArgumentNullException.ThrowIfNull(
            visualElements);

        if (layout.PhysicalPageNumber !=
            sourcePage.PhysicalPageNumber)
        {
            throw new ArgumentException(
                "Layout result must belong to the source page.",
                nameof(layout));
        }

        if (sourcePage.Blocks.Count ==
            0)
        {
            throw new InvalidDataException(
                $"Native/layout visual merge for page " +
                $"{sourcePage.PhysicalPageNumber} requires native text blocks.");
        }

        var resolvedVisuals =
            ValidateAndOrderVisuals(
                sourcePage,
                layout,
                visualElements);

        if (resolvedVisuals.Length ==
            0)
        {
            return NativeHybridPageAssembler
                .Assemble(
                    sourcePage);
        }

        var sourceBlocksBySequence =
            BuildSourceBlockIndex(
                sourcePage);

        var ordersByBlock =
            sourceBlocksBySequence
                .Keys
                .ToDictionary(
                    sourceSequence =>
                        sourceSequence,
                    _ =>
                        new HashSet<int>());

        var textTargets =
            layout.Observations
                .Where(
                    observation =>
                        LayoutTextPolicy
                            .IsTextRecognitionCandidate(
                                observation.Kind))
                .ToArray();

        var pairings =
            NativeLayoutTextPairer
                .Pair(
                    sourcePage.Blocks,
                    textTargets);

        RejectAmbiguousPairings(
            sourcePage,
            pairings);

        AccumulateLayoutOrders(
            sourcePage,
            sourceBlocksBySequence,
            ordersByBlock,
            pairings);

        var placements =
            sourcePage.Blocks
                .Select(
                    block =>
                        CreatePlacement(
                            sourcePage,
                            block,
                            ordersByBlock[
                                block.SourceSequence],
                            resolvedVisuals))
                .ToArray();

        var merged =
            BuildDenseHybridStream(
                sourcePage,
                resolvedVisuals,
                placements);

        return HybridDocumentAssembler
            .AssemblePage(
                sourcePage,
                merged);
    }

    private static HybridDocumentElement[] ValidateAndOrderVisuals(
        DocumentExtractionPage sourcePage,
        LayoutAnalysisResult layout,
        IReadOnlyList<HybridDocumentElement> visualElements)
    {
        var resolved =
            visualElements.ToArray();

        if (resolved.Any(
                visual =>
                    visual is null))
        {
            throw new ArgumentException(
                "Visual elements cannot contain null values.",
                nameof(visualElements));
        }

        foreach (var visual in
                 resolved)
        {
            if (visual.Kind !=
                HybridDocumentElementKind.Visual)
            {
                throw new ArgumentException(
                    "Native/layout visual merge accepts only Visual hybrid elements.",
                    nameof(visualElements));
            }

            if (visual.PhysicalPageNumber !=
                sourcePage.PhysicalPageNumber)
            {
                throw new ArgumentException(
                    "Every visual element must belong to the source page.",
                    nameof(visualElements));
            }

            var observation =
                visual.LayoutObservation ??
                throw new InvalidDataException(
                    "Preserved visual has no layout observation.");

            if (visual.PreservedVisual is null)
            {
                throw new InvalidDataException(
                    "Visual hybrid element has no preserved visual evidence.");
            }

            if (observation.Kind !=
                LayoutObservationKind.Figure)
            {
                throw new InvalidDataException(
                    "Preserved visual must remain backed by Figure layout evidence.");
            }

            if (observation.ReadingOrder is null)
            {
                throw new InvalidDataException(
                    "Preserved visual Figure requires explicit layout reading order.");
            }

            if (visual.ReadingOrder !=
                observation.ReadingOrder.Value)
            {
                throw new InvalidDataException(
                    "Incoming visual element reading order differs from its Figure evidence.");
            }

            if (!layout.Observations.Any(
                    candidate =>
                        ReferenceEquals(
                            candidate,
                            observation)))
            {
                throw new InvalidDataException(
                    "Preserved visual Figure is not part of the supplied layout result.");
            }
        }

        var duplicateVisualOrder =
            resolved
                .GroupBy(
                    visual =>
                        visual.ReadingOrder)
                .FirstOrDefault(
                    group =>
                        group.Count() >
                        1);

        if (duplicateVisualOrder is not null)
        {
            throw new InvalidDataException(
                $"Preserved visuals have duplicate layout reading order " +
                $"{duplicateVisualOrder.Key}.");
        }

        return resolved
            .OrderBy(
                visual =>
                    visual.ReadingOrder)
            .ThenBy(
                visual =>
                    visual.LayoutObservation!
                        .ObservationSequence)
            .ToArray();
    }

    private static IReadOnlyDictionary<int, DocumentTextBlock>
        BuildSourceBlockIndex(
            DocumentExtractionPage sourcePage)
    {
        var duplicateSequence =
            sourcePage.Blocks
                .GroupBy(
                    block =>
                        block.SourceSequence)
                .FirstOrDefault(
                    group =>
                        group.Count() >
                        1);

        if (duplicateSequence is not null)
        {
            throw new InvalidDataException(
                $"Native page {sourcePage.PhysicalPageNumber} contains duplicate " +
                $"source block sequence {duplicateSequence.Key}.");
        }

        return sourcePage.Blocks
            .ToDictionary(
                block =>
                    block.SourceSequence);
    }

    private static void RejectAmbiguousPairings(
        DocumentExtractionPage sourcePage,
        IReadOnlyList<NativeLayoutTextPairing> pairings)
    {
        var ambiguous =
            pairings
                .FirstOrDefault(
                    pairing =>
                        pairing.Status ==
                        NativeLayoutTextPairingStatus
                            .AmbiguousWordOwnership);

        if (ambiguous is not null)
        {
            throw new InvalidDataException(
                $"Native/layout visual merge for page " +
                $"{sourcePage.PhysicalPageNumber} has ambiguous native word ownership " +
                $"at layout observation " +
                $"{ambiguous.TargetLayoutObservation.ObservationSequence}.");
        }
    }

    private static void AccumulateLayoutOrders(
        DocumentExtractionPage sourcePage,
        IReadOnlyDictionary<int, DocumentTextBlock> sourceBlocksBySequence,
        IReadOnlyDictionary<int, HashSet<int>> ordersByBlock,
        IReadOnlyList<NativeLayoutTextPairing> pairings)
    {
        foreach (var pairing in
                 pairings)
        {
            if (pairing.Status !=
                NativeLayoutTextPairingStatus.Comparable)
            {
                continue;
            }

            var readingOrder =
                pairing.TargetLayoutObservation
                    .ReadingOrder ??
                throw new InvalidDataException(
                    $"Comparable layout text observation " +
                    $"{pairing.TargetLayoutObservation.ObservationSequence} on page " +
                    $"{sourcePage.PhysicalPageNumber} has no reading order.");

            var evidence =
                pairing.ComparableNativeEvidence ??
                throw new InvalidDataException(
                    "Comparable native/layout pairing has no comparable evidence.");

            foreach (var extent in
                     evidence.Extents)
            {
                if (!sourceBlocksBySequence.TryGetValue(
                        extent.SourceBlock.SourceSequence,
                        out var sourceBlock) ||
                    !ReferenceEquals(
                        sourceBlock,
                        extent.SourceBlock))
                {
                    throw new InvalidDataException(
                        "Comparable native/layout pairing references a foreign source block.");
                }

                ordersByBlock[
                        extent.SourceBlock.SourceSequence]
                    .Add(
                        readingOrder);
            }
        }
    }

    private static NativeBlockPlacement CreatePlacement(
        DocumentExtractionPage sourcePage,
        DocumentTextBlock block,
        IReadOnlyCollection<int> targetOrders,
        IReadOnlyList<HybridDocumentElement> orderedVisuals)
    {
        if (targetOrders.Count ==
            0)
        {
            return CreateGeometryFallbackPlacement(
                sourcePage,
                block,
                orderedVisuals);
        }

        var visualOrders =
            orderedVisuals
                .Select(
                    visual =>
                        visual.ReadingOrder)
                .ToArray();

        foreach (var visualOrder in
                 visualOrders)
        {
            if (targetOrders.Contains(
                    visualOrder))
            {
                throw new InvalidDataException(
                    $"Native block {block.SourceSequence} on page " +
                    $"{sourcePage.PhysicalPageNumber} shares layout reading order " +
                    $"{visualOrder} with a preserved visual.");
            }

            var hasBefore =
                targetOrders.Any(
                    targetOrder =>
                        targetOrder <
                        visualOrder);

            var hasAfter =
                targetOrders.Any(
                    targetOrder =>
                        targetOrder >
                        visualOrder);

            if (hasBefore &&
                hasAfter)
            {
                if (TryResolveGeometryVisualBand(
                        block,
                        orderedVisuals,
                        out var geometryVisualBand))
                {
                    return new NativeBlockPlacement(
                        block,
                        geometryVisualBand);
                }

                throw new InvalidDataException(
                    $"Native block {block.SourceSequence} on page " +
                    $"{sourcePage.PhysicalPageNumber} straddles preserved visual " +
                    $"reading order {visualOrder}; whole-block merge would be unsafe.");
            }
        }

        var visualBand =
            visualOrders.Count(
                visualOrder =>
                    targetOrders.All(
                        targetOrder =>
                            targetOrder >
                            visualOrder));

        return new NativeBlockPlacement(
            block,
            visualBand);
    }

    private static NativeBlockPlacement CreateGeometryFallbackPlacement(
        DocumentExtractionPage sourcePage,
        DocumentTextBlock block,
        IReadOnlyList<HybridDocumentElement> orderedVisuals)
    {
        var visualBand =
            0;

        var encounteredVisualAfterBlock =
            false;

        foreach (var visual in
                 orderedVisuals)
        {
            var visualBounds =
                visual.LayoutObservation!
                    .Bounds;

            if (block.Bounds.Top >=
                visualBounds.Bottom)
            {
                if (encounteredVisualAfterBlock)
                {
                    throw new InvalidDataException(
                        $"Native block {block.SourceSequence} on page " +
                        $"{sourcePage.PhysicalPageNumber} has no deterministic " +
                        "layout text ownership and preserved-visual geometry " +
                        "conflicts with layout reading order.");
                }

                visualBand++;
                continue;
            }

            if (block.Bounds.Bottom <=
                visualBounds.Top)
            {
                encounteredVisualAfterBlock =
                    true;
                continue;
            }

            throw new InvalidDataException(
                $"Native block {block.SourceSequence} on page " +
                $"{sourcePage.PhysicalPageNumber} has no deterministic layout " +
                $"text ownership and overlaps preserved visual reading order " +
                $"{visual.ReadingOrder}; geometry fallback would be unsafe.");
        }

        return new NativeBlockPlacement(
            block,
            visualBand);
    }

    private static bool TryResolveGeometryVisualBand(
        DocumentTextBlock block,
        IReadOnlyList<HybridDocumentElement> orderedVisuals,
        out int visualBand)
    {
        visualBand =
            0;

        var encounteredVisualAfterBlock =
            false;

        foreach (var visual in
                 orderedVisuals)
        {
            var visualBounds =
                visual.LayoutObservation!
                    .Bounds;

            if (block.Bounds.Top >=
                visualBounds.Bottom)
            {
                if (encounteredVisualAfterBlock)
                {
                    return false;
                }

                visualBand++;
                continue;
            }

            if (block.Bounds.Bottom <=
                visualBounds.Top)
            {
                encounteredVisualAfterBlock =
                    true;
                continue;
            }

            return false;
        }

        return true;
    }

    private static IReadOnlyList<HybridDocumentElement> BuildDenseHybridStream(
        DocumentExtractionPage sourcePage,
        IReadOnlyList<HybridDocumentElement> orderedVisuals,
        IReadOnlyList<NativeBlockPlacement> placements)
    {
        var merged =
            new List<HybridDocumentElement>(
                sourcePage.Blocks.Count +
                orderedVisuals.Count);

        var nextReadingOrder =
            0;

        for (var band =
                 0;
             band <=
             orderedVisuals.Count;
             band++)
        {
            foreach (var placement in
                     placements
                         .Where(
                             item =>
                                 item.VisualBand ==
                                 band)
                         .OrderBy(
                             item =>
                                 item.Block.ReadingOrder is null)
                         .ThenBy(
                             item =>
                                 item.Block.ReadingOrder ??
                                 int.MaxValue)
                         .ThenBy(
                             item =>
                                 item.Block.SourceSequence))
            {
                merged.Add(
                    HybridDocumentElementFactory
                        .FromNativeWithReadingOrder(
                            sourcePage.PhysicalPageNumber,
                            placement.Block,
                            nextReadingOrder));

                nextReadingOrder++;
            }

            if (band >=
                orderedVisuals.Count)
            {
                continue;
            }

            merged.Add(
                ReindexVisual(
                    orderedVisuals[band],
                    nextReadingOrder));

            nextReadingOrder++;
        }

        return merged;
    }

    private static HybridDocumentElement ReindexVisual(
        HybridDocumentElement visual,
        int readingOrder) =>
        new(
            visual.PhysicalPageNumber,
            readingOrder,
            visual.Kind,
            visual.Bounds,
            visual.Text,
            visual.TextOrigin,
            visual.NativeBlock,
            visual.LayoutObservation,
            visual.Reconciliation,
            visual.PreservedVisual);

    private sealed record NativeBlockPlacement(
        DocumentTextBlock Block,
        int VisualBand);

    #endregion
}
