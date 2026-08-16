using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Engine.Segmentation;

namespace DocumentProcessing.Engine.Orchestration;

/// <summary>
/// Enriches low-level visual raster observations with deterministic native
/// document structure and produces complete <see cref="VisualEvidenceObservation"/>
/// instances.
///
/// This component promotes the frozen Phase 21E.1F structural algorithms.
/// It produces evidence only. It does not select visual disposition, routing,
/// OCR or layout execution.
/// </summary>
public sealed class DefaultVisualStructuralEvidenceEnricher
{
    private const double StrongHeadingDistance =
        0.025;

    private const double PossibleHeadingDistance =
        0.060;

    private const double StrongVerticalOverlap =
        0.45;

    private const double MinimumHeadingVisualHeightRatio =
        0.35;

    private const double MaximumHeadingVisualHeightRatio =
        3.50;

    private const double FullContainmentTolerance =
        0.003;

    private const double ContainedIntersectionMinimumRatio =
        0.75;

    private const int ParagraphLikeMinimumWordCount =
        12;

    private const int ParagraphLikeMinimumCharacterCount =
        80;

    private const double TextRichContainedPageWordMinimumRatio =
        0.08;

    private const double CaptionMaximumCandidateGap =
        0.08;

    private const double StrongCaptionMaximumGap =
        0.06;

    private const double PossibleCaptionMaximumGap =
        0.025;

    private const double LexicalCaptionCenterTolerance =
        0.03;

    private const double BlockCaptionCenterTolerance =
        0.02;

    private const double CaptionMinimumHorizontalOverlap =
        0.10;

    private const double StrongCaptionMinimumHorizontalOverlap =
        0.15;

    private const double PossibleCaptionMinimumHorizontalOverlap =
        0.50;

    private const int CaptionMinimumWordCount =
        2;

    private const int CaptionMaximumWordCount =
        50;

    private const int CaptionMaximumCharacterCount =
        320;

    public IReadOnlyList<PageVisualEvidenceObservations> Enrich(
        DocumentExtractionResult extraction,
        DocumentTextNormalizationResult normalization,
        IReadOnlyList<PageVisualRasterObservations> rasterObservations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            normalization);

        ArgumentNullException.ThrowIfNull(
            rasterObservations);

        if (!ReferenceEquals(
                normalization.SourceExtraction,
                extraction))
        {
            throw new InvalidDataException(
                "Structural visual enrichment requires normalization derived from the supplied extraction result.");
        }

        if (normalization.Pages.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Normalization contains {normalization.Pages.Count} page(s), but extraction contains " +
                $"{extraction.Pages.Count} page(s).");
        }

        if (rasterObservations.Count !=
            extraction.Pages.Count)
        {
            throw new InvalidDataException(
                $"Raster observations contain {rasterObservations.Count} page(s), but extraction contains " +
                $"{extraction.Pages.Count} page(s).");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var headingEvaluator =
            new HeadingEvidenceEvaluator(
                normalization);

        var result =
            new PageVisualEvidenceObservations[
                extraction.Pages.Count];

        for (var pageIndex = 0;
             pageIndex <
             extraction.Pages.Count;
             pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extractionPage =
                extraction.Pages[
                    pageIndex];

            var normalizedPage =
                normalization.Pages[
                    pageIndex];

            var rasterPage =
                rasterObservations[
                    pageIndex];

            ValidatePageAlignment(
                extractionPage,
                normalizedPage,
                rasterPage,
                pageIndex);

            var structuralBlocks =
                BuildStructuralBlocks(
                    normalizedPage,
                    headingEvaluator);

            var visualElements =
                new VisualEvidenceObservation[
                    rasterPage.VisualElements.Count];

            for (var visualIndex = 0;
                 visualIndex <
                 rasterPage.VisualElements.Count;
                 visualIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rasterObservation =
                    rasterPage.VisualElements[
                        visualIndex];

                if (rasterObservation.SourceVisualIndex !=
                    visualIndex)
                {
                    throw new InvalidDataException(
                        $"Physical page {extractionPage.PhysicalPageNumber} raster observations must preserve " +
                        $"source visual order 0..{Math.Max(0, extractionPage.RasterImageCount - 1)}.");
                }

                visualElements[
                    visualIndex] =
                    EnrichVisual(
                        rasterObservation,
                        structuralBlocks,
                        extractionPage.Words,
                        extractionPage.WordCount);
            }

            result[
                pageIndex] =
                new PageVisualEvidenceObservations(
                    extractionPage.PhysicalPageNumber,
                    visualElements);
        }

        return result;
    }

    #region Page and structural-block preparation

    private static void ValidatePageAlignment(
        DocumentExtractionPage extractionPage,
        NormalizedDocumentPage normalizedPage,
        PageVisualRasterObservations rasterPage,
        int pageIndex)
    {
        if (extractionPage.PhysicalPageNumber !=
            normalizedPage.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                $"Normalized page at index {pageIndex} refers to physical page " +
                $"{normalizedPage.PhysicalPageNumber}; expected {extractionPage.PhysicalPageNumber}.");
        }

        if (!ReferenceEquals(
                normalizedPage.SourcePage,
                extractionPage))
        {
            throw new InvalidDataException(
                $"Normalized physical page {normalizedPage.PhysicalPageNumber} is not derived from the aligned extraction page instance.");
        }

        if (rasterPage.PhysicalPageNumber !=
            extractionPage.PhysicalPageNumber)
        {
            throw new InvalidDataException(
                $"Raster observation page at index {pageIndex} refers to physical page " +
                $"{rasterPage.PhysicalPageNumber}; expected {extractionPage.PhysicalPageNumber}.");
        }

        if (rasterPage.VisualElements.Count !=
            extractionPage.RasterImageCount)
        {
            throw new InvalidDataException(
                $"Physical page {extractionPage.PhysicalPageNumber} reports " +
                $"{extractionPage.RasterImageCount} source raster image occurrence(s), but structural enrichment " +
                $"received {rasterPage.VisualElements.Count} raster observation(s).");
        }
    }

    private static IReadOnlyList<StructuralBlock> BuildStructuralBlocks(
        NormalizedDocumentPage page,
        HeadingEvidenceEvaluator headingEvaluator)
    {
        var blocks =
            new List<StructuralBlock>();

        foreach (var normalizedBlock in
                 page.Blocks)
        {
            if (normalizedBlock.IsExcluded ||
                string.IsNullOrWhiteSpace(
                    normalizedBlock.Text))
            {
                continue;
            }

            var sourceBlock =
                normalizedBlock.SourceBlock;

            var normalizedText =
                normalizedBlock.Text;

            blocks.Add(
                new StructuralBlock(
                    sourceBlock.SourceSequence,
                    sourceBlock.Bounds,
                    headingEvaluator.IsHeading(
                        normalizedBlock),
                    EstimateWordCount(
                        normalizedText),
                    normalizedText.Length,
                    normalizedText));
        }

        return blocks;
    }

    private static int EstimateWordCount(
        string text) =>
        text.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries)
            .Length;

    #endregion

    #region Visual enrichment

    private static VisualEvidenceObservation EnrichVisual(
        VisualRasterObservation raster,
        IReadOnlyList<StructuralBlock> blocks,
        IReadOnlyList<DocumentWord> nativeWords,
        int pageNativeWordCount)
    {
        if (raster.ForegroundState !=
                VisualForegroundState.Measured ||
            raster.EffectiveVisualBounds is null)
        {
            return new VisualEvidenceObservation(
                raster.SourceVisualIndex,
                raster.ForegroundState,
                raster.ForegroundPixelRatio,
                raster.PixelInteraction,
                raster.NativeWordsTouchedRatio,
                raster.SignificantComponentCount,
                raster.EffectiveVisualAreaRatio,
                HeadingAssociationEvidenceKind.NotMeasured,
                NativeTextContainmentEvidenceKind.NotMeasured,
                CaptionAssociationEvidenceKind.NotMeasured);
        }

        var effectiveAreaRatio =
            raster.EffectiveVisualAreaRatio;

        if (effectiveAreaRatio is null ||
            !double.IsFinite(
                effectiveAreaRatio.Value) ||
            effectiveAreaRatio.Value <=
                0 ||
            effectiveAreaRatio.Value >
                1)
        {
            return FailClosedAsUnknown(
                raster.SourceVisualIndex);
        }

        var effectiveVisual =
            raster.EffectiveVisualBounds.Value;

        var headingAssociation =
            AnalyzeHeadingAssociation(
                effectiveVisual,
                blocks,
                raster.PixelInteraction);

        var textContainment =
            AnalyzeTextContainment(
                effectiveVisual,
                blocks,
                pageNativeWordCount);

        var captionAssociation =
            AnalyzeCaptionAssociation(
                effectiveVisual,
                blocks,
                nativeWords);

        return new VisualEvidenceObservation(
            raster.SourceVisualIndex,
            raster.ForegroundState,
            raster.ForegroundPixelRatio,
            raster.PixelInteraction,
            raster.NativeWordsTouchedRatio,
            raster.SignificantComponentCount,
            effectiveAreaRatio,
            headingAssociation,
            textContainment,
            captionAssociation);
    }

    private static VisualEvidenceObservation FailClosedAsUnknown(
        int sourceVisualIndex) =>
        new(
            sourceVisualIndex,
            VisualForegroundState.Unavailable,
            foregroundPixelRatio:
                null,
            VisualPixelInteractionKind.NotMeasured,
            nativeWordsTouchedRatio:
                0,
            significantComponentCount:
                null,
            effectiveVisualAreaRatio:
                null,
            HeadingAssociationEvidenceKind.NotMeasured,
            NativeTextContainmentEvidenceKind.NotMeasured,
            CaptionAssociationEvidenceKind.NotMeasured);

    #endregion

    #region Heading association

    private static HeadingAssociationEvidenceKind AnalyzeHeadingAssociation(
        NormalizedRectangle visual,
        IReadOnlyList<StructuralBlock> blocks,
        VisualPixelInteractionKind pixelInteraction)
    {
        var nearestHeading =
            FindNearest(
                visual,
                blocks
                    .Where(
                        block =>
                            block.IsHeading)
                    .ToArray());

        if (nearestHeading is null)
        {
            return HeadingAssociationEvidenceKind.NoStrongAssociation;
        }

        var headingDistance =
            RectangleDistance(
                visual,
                nearestHeading.Bounds);

        var headingHeight =
            Height(
                nearestHeading.Bounds);

        var verticalOverlap =
            OverlapRatio(
                visual.Top,
                visual.Bottom,
                nearestHeading.Bounds.Top,
                nearestHeading.Bounds.Bottom);

        var heightRatio =
            headingHeight <=
                0
                ? (double?)null
                : Height(
                    visual) /
                  headingHeight;

        var lowTextInteraction =
            pixelInteraction is
                VisualPixelInteractionKind.NoForegroundWordIntersection or
                VisualPixelInteractionKind.LowForegroundWordInteraction or
                VisualPixelInteractionKind.BlankCanvas;

        if (heightRatio.HasValue &&
            headingDistance <=
                StrongHeadingDistance &&
            verticalOverlap >=
                StrongVerticalOverlap &&
            heightRatio.Value >=
                MinimumHeadingVisualHeightRatio &&
            heightRatio.Value <=
                MaximumHeadingVisualHeightRatio &&
            lowTextInteraction)
        {
            return HeadingAssociationEvidenceKind.StrongAdjacentVisual;
        }

        if (headingDistance <=
            PossibleHeadingDistance)
        {
            return HeadingAssociationEvidenceKind.PossibleAdjacentVisual;
        }

        return HeadingAssociationEvidenceKind.NoStrongAssociation;
    }

    private static StructuralBlock? FindNearest(
        NormalizedRectangle source,
        IReadOnlyList<StructuralBlock> targets)
    {
        if (targets.Count ==
            0)
        {
            return null;
        }

        return targets
            .OrderBy(
                target =>
                    RectangleDistance(
                        source,
                        target.Bounds))
            .ThenBy(
                target =>
                    target.SourceSequence)
            .First();
    }

    private static double RectangleDistance(
        NormalizedRectangle left,
        NormalizedRectangle right)
    {
        var dx =
            AxisGap(
                left.Left,
                left.Right,
                right.Left,
                right.Right);

        var dy =
            AxisGap(
                left.Top,
                left.Bottom,
                right.Top,
                right.Bottom);

        return Math.Sqrt(
            dx *
            dx +
            dy *
            dy);
    }

    private static double AxisGap(
        double a0,
        double a1,
        double b0,
        double b1)
    {
        if (a1 <
            b0)
        {
            return b0 -
                   a1;
        }

        if (b1 <
            a0)
        {
            return a0 -
                   b1;
        }

        return 0;
    }

    private static double OverlapRatio(
        double a0,
        double a1,
        double b0,
        double b1)
    {
        var overlap =
            Math.Max(
                0,
                Math.Min(
                    a1,
                    b1) -
                Math.Max(
                    a0,
                    b0));

        var denominator =
            Math.Min(
                a1 -
                a0,
                b1 -
                b0);

        return denominator <=
                0
                ? 0
                : overlap /
                  denominator;
    }

    #endregion

    #region Native-text containment

    private static NativeTextContainmentEvidenceKind AnalyzeTextContainment(
        NormalizedRectangle visual,
        IReadOnlyList<StructuralBlock> blocks,
        int pageNativeWordCount)
    {
        var containedHeadingBlockCount =
            0;

        var containedBodyBlockCount =
            0;

        var containedParagraphLikeBlockCount =
            0;

        var containedWordCount =
            0;

        var containedHeadingWordCount =
            0;

        var containedBodyWordCount =
            0;

        foreach (var block in
                 blocks)
        {
            var intersectionRatio =
                IntersectionAreaRatio(
                    visual,
                    block.Bounds);

            var centerContained =
                ContainsPoint(
                    visual,
                    CenterX(
                        block.Bounds),
                    CenterY(
                        block.Bounds));

            var fullyContained =
                ContainsRectangle(
                    visual,
                    block.Bounds,
                    FullContainmentTolerance);

            var countsAsContained =
                centerContained ||
                fullyContained ||
                intersectionRatio >=
                    ContainedIntersectionMinimumRatio;

            if (!countsAsContained)
            {
                continue;
            }

            containedWordCount +=
                block.WordCount;

            if (block.IsHeading)
            {
                containedHeadingBlockCount++;

                containedHeadingWordCount +=
                    block.WordCount;
            }
            else
            {
                containedBodyBlockCount++;

                containedBodyWordCount +=
                    block.WordCount;

                if (block.WordCount >=
                        ParagraphLikeMinimumWordCount ||
                    block.CharacterCount >=
                        ParagraphLikeMinimumCharacterCount)
                {
                    containedParagraphLikeBlockCount++;
                }
            }
        }

        var containedPageWordRatio =
            pageNativeWordCount <=
                0
                ? 0
                : containedWordCount /
                  (double)pageNativeWordCount;

        if (containedWordCount ==
            0)
        {
            return NativeTextContainmentEvidenceKind.NoContainedNativeText;
        }

        if (containedHeadingBlockCount >
                0 &&
            containedBodyWordCount <=
                Math.Max(
                    3,
                    containedHeadingWordCount /
                    2))
        {
            return NativeTextContainmentEvidenceKind.HeadingDominatedContainedText;
        }

        if (containedBodyBlockCount >=
                2 ||
            containedParagraphLikeBlockCount >=
                1 ||
            containedPageWordRatio >=
                TextRichContainedPageWordMinimumRatio)
        {
            return NativeTextContainmentEvidenceKind.TextRichContainer;
        }

        return NativeTextContainmentEvidenceKind.SparseContainedText;
    }

    #endregion

    #region Caption association

    private static CaptionAssociationEvidenceKind AnalyzeCaptionAssociation(
        NormalizedRectangle visual,
        IReadOnlyList<StructuralBlock> blocks,
        IReadOnlyList<DocumentWord> words)
    {
        var lexicalWordCandidates =
            words
                .Where(
                    word =>
                        IsCaptionLeadWord(
                            word.Text))
                .Select(
                    word =>
                    {
                        var relationship =
                            RelativeVerticalRelationship(
                                visual,
                                word.Bounds);

                        var centerAligned =
                            CenterX(
                                word.Bounds) >=
                                visual.Left -
                                LexicalCaptionCenterTolerance &&
                            CenterX(
                                word.Bounds) <=
                                visual.Right +
                                LexicalCaptionCenterTolerance;

                        return new CaptionWordCandidate(
                            word,
                            relationship.Position,
                            relationship.Gap,
                            centerAligned);
                    })
                .Where(
                    candidate =>
                        candidate.Position is not null &&
                        candidate.Gap <=
                            CaptionMaximumCandidateGap &&
                        candidate.CenterAligned)
                .OrderBy(
                    candidate =>
                        candidate.Gap)
                .ThenBy(
                    candidate =>
                        candidate.Word.Bounds.Top)
                .ToArray();

        if (lexicalWordCandidates.Length >
            0)
        {
            return lexicalWordCandidates[0].Gap <=
                    StrongCaptionMaximumGap
                ? CaptionAssociationEvidenceKind.StrongAssociation
                : CaptionAssociationEvidenceKind.PossibleAssociation;
        }

        var candidates =
            new List<CaptionCandidate>();

        foreach (var block in
                 blocks)
        {
            if (block.IsHeading)
            {
                continue;
            }

            var relationship =
                RelativeVerticalRelationship(
                    visual,
                    block.Bounds);

            if (relationship.Position is null ||
                relationship.Gap >
                    CaptionMaximumCandidateGap)
            {
                continue;
            }

            var horizontalOverlap =
                HorizontalOverlapRatio(
                    visual,
                    block.Bounds);

            var centerAligned =
                CenterX(
                    block.Bounds) >=
                    visual.Left -
                    BlockCaptionCenterTolerance &&
                CenterX(
                    block.Bounds) <=
                    visual.Right +
                    BlockCaptionCenterTolerance;

            var lexicalHint =
                HasCaptionLexicalHint(
                    block.FullText);

            if (horizontalOverlap <
                    CaptionMinimumHorizontalOverlap &&
                !centerAligned &&
                !lexicalHint)
            {
                continue;
            }

            candidates.Add(
                new CaptionCandidate(
                    block,
                    relationship.Position.Value,
                    relationship.Gap,
                    horizontalOverlap,
                    centerAligned,
                    lexicalHint));
        }

        if (candidates.Count ==
            0)
        {
            return CaptionAssociationEvidenceKind.NoAssociation;
        }

        var candidate =
            candidates
                .OrderByDescending(
                    item =>
                        item.HasLexicalHint)
                .ThenBy(
                    item =>
                        item.Gap)
                .ThenByDescending(
                    item =>
                        item.HorizontalOverlapRatio)
                .ThenBy(
                    item =>
                        item.Block.SourceSequence)
                .First();

        var shortCaptionLikeText =
            candidate.Block.WordCount >=
                CaptionMinimumWordCount &&
            candidate.Block.WordCount <=
                CaptionMaximumWordCount &&
            candidate.Block.CharacterCount <=
                CaptionMaximumCharacterCount;

        if (candidate.HasLexicalHint &&
            shortCaptionLikeText &&
            candidate.Gap <=
                StrongCaptionMaximumGap &&
            (
                candidate.HorizontalOverlapRatio >=
                    StrongCaptionMinimumHorizontalOverlap ||
                candidate.CenterAligned
            ))
        {
            return CaptionAssociationEvidenceKind.StrongAssociation;
        }

        if (shortCaptionLikeText &&
            candidate.Gap <=
                PossibleCaptionMaximumGap &&
            candidate.HorizontalOverlapRatio >=
                PossibleCaptionMinimumHorizontalOverlap)
        {
            return CaptionAssociationEvidenceKind.PossibleAssociation;
        }

        return CaptionAssociationEvidenceKind.NoStrongAssociation;
    }

    private static VerticalRelationship RelativeVerticalRelationship(
        NormalizedRectangle visual,
        NormalizedRectangle target)
    {
        if (target.Top >=
            visual.Bottom)
        {
            return new VerticalRelationship(
                RelativeVerticalPosition.Below,
                target.Top -
                visual.Bottom);
        }

        if (target.Bottom <=
            visual.Top)
        {
            return new VerticalRelationship(
                RelativeVerticalPosition.Above,
                visual.Top -
                target.Bottom);
        }

        return new VerticalRelationship(
            Position:
                null,
            Gap:
                0);
    }

    private static bool IsCaptionLeadWord(
        string text)
    {
        var trimmed =
            text.Trim()
                .TrimEnd(
                    ':',
                    '.');

        return trimmed.Equals(
                   "Figure",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals(
                   "Fig",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals(
                   "Table",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals(
                   "Plate",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals(
                   "Exhibit",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCaptionLexicalHint(
        string text)
    {
        var trimmed =
            text.TrimStart();

        return trimmed.StartsWith(
                   "Figure ",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(
                   "Fig. ",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(
                   "Fig ",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(
                   "Table ",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(
                   "Plate ",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(
                   "Exhibit ",
                   StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Geometry helpers

    private static bool ContainsPoint(
        NormalizedRectangle container,
        double x,
        double y) =>
        x >=
            container.Left &&
        x <=
            container.Right &&
        y >=
            container.Top &&
        y <=
            container.Bottom;

    private static bool ContainsRectangle(
        NormalizedRectangle container,
        NormalizedRectangle target,
        double tolerance) =>
        target.Left >=
            container.Left -
            tolerance &&
        target.Right <=
            container.Right +
            tolerance &&
        target.Top >=
            container.Top -
            tolerance &&
        target.Bottom <=
            container.Bottom +
            tolerance;

    private static double IntersectionAreaRatio(
        NormalizedRectangle source,
        NormalizedRectangle target)
    {
        var intersectionWidth =
            Math.Max(
                0,
                Math.Min(
                    source.Right,
                    target.Right) -
                Math.Max(
                    source.Left,
                    target.Left));

        var intersectionHeight =
            Math.Max(
                0,
                Math.Min(
                    source.Bottom,
                    target.Bottom) -
                Math.Max(
                    source.Top,
                    target.Top));

        var targetArea =
            Width(
                target) *
            Height(
                target);

        return targetArea <=
                0
                ? 0
                : intersectionWidth *
                  intersectionHeight /
                  targetArea;
    }

    private static double HorizontalOverlapRatio(
        NormalizedRectangle source,
        NormalizedRectangle target)
    {
        var overlap =
            Math.Max(
                0,
                Math.Min(
                    source.Right,
                    target.Right) -
                Math.Max(
                    source.Left,
                    target.Left));

        var denominator =
            Math.Min(
                Width(
                    source),
                Width(
                    target));

        return denominator <=
                0
                ? 0
                : overlap /
                  denominator;
    }

    private static double CenterX(
        NormalizedRectangle rectangle) =>
        (
            rectangle.Left +
            rectangle.Right
        ) /
        2;

    private static double CenterY(
        NormalizedRectangle rectangle) =>
        (
            rectangle.Top +
            rectangle.Bottom
        ) /
        2;

    private static double Width(
        NormalizedRectangle rectangle) =>
        Math.Max(
            0,
            rectangle.Right -
            rectangle.Left);

    private static double Height(
        NormalizedRectangle rectangle) =>
        Math.Max(
            0,
            rectangle.Bottom -
            rectangle.Top);

    #endregion

    #region Private evidence records

    private sealed record StructuralBlock(
        int SourceSequence,
        NormalizedRectangle Bounds,
        bool IsHeading,
        int WordCount,
        int CharacterCount,
        string FullText);

    private sealed record CaptionWordCandidate(
        DocumentWord Word,
        RelativeVerticalPosition? Position,
        double Gap,
        bool CenterAligned);

    private sealed record CaptionCandidate(
        StructuralBlock Block,
        RelativeVerticalPosition Position,
        double Gap,
        double HorizontalOverlapRatio,
        bool CenterAligned,
        bool HasLexicalHint);

    private readonly record struct VerticalRelationship(
        RelativeVerticalPosition? Position,
        double Gap);

    private enum RelativeVerticalPosition
    {
        Above,
        Below
    }

    #endregion
}
