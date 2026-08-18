using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using DocumentProcessing.Engine.Normalization;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Engine.Planning;

namespace DocumentProcessing.UnitTests.Planning;

public sealed class DefaultVisualStructuralEvidenceEnricherTests
{
    [Fact]
    public void Enrich_StrongHeadingAssociation_UsesProductionHeadingEvidence()
    {
        var fixture =
            HeadingFixture();

        var observation =
            EnrichSingle(
                fixture,
                Measured(
                    effectiveBounds:
                        new NormalizedRectangle(
                            0.280,
                            0.205,
                            0.295,
                            0.245),
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    foregroundPixelRatio:
                        0.001,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        1));

        Assert.Equal(
            HeadingAssociationEvidenceKind.StrongAdjacentVisual,
            observation.HeadingAssociation);

        Assert.Equal(
            NativeTextContainmentEvidenceKind.NoContainedNativeText,
            observation.TextContainment);

        Assert.Equal(
            CaptionAssociationEvidenceKind.NoAssociation,
            observation.CaptionAssociation);

        Assert.Equal(
            VisualEvidenceKind.SmallHeadingAssociatedVisual,
            new DefaultVisualEvidenceAssessor()
                .Assess(
                    observation)
                .Kind);
    }

    [Fact]
    public void Enrich_HeadingDominatedContainedText_PreservesSemanticHeadingAsTextEvidence()
    {
        var fixture =
            HeadingFixture();

        var observation =
            EnrichSingle(
                fixture,
                Measured(
                    effectiveBounds:
                        new NormalizedRectangle(
                            0.250,
                            0.150,
                            0.750,
                            0.300),
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    foregroundPixelRatio:
                        0.025,
                    nativeWordsTouchedRatio:
                        0.10,
                    significantComponentCount:
                        3));

        Assert.Equal(
            NativeTextContainmentEvidenceKind.HeadingDominatedContainedText,
            observation.TextContainment);

        Assert.Equal(
            VisualEvidenceKind.HeadingBackplateOrPresentation,
            new DefaultVisualEvidenceAssessor()
                .Assess(
                    observation)
                .Kind);
    }

    [Fact]
    public void Enrich_ParagraphLikeContainedBody_ProducesTextRichContainerEvidence()
    {
        var fixture =
            TextRichFixture();

        var observation =
            EnrichSingle(
                fixture,
                Measured(
                    effectiveBounds:
                        new NormalizedRectangle(
                            0.080,
                            0.360,
                            0.920,
                            0.640),
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    foregroundPixelRatio:
                        0.18,
                    nativeWordsTouchedRatio:
                        0.80,
                    significantComponentCount:
                        4));

        Assert.Equal(
            NativeTextContainmentEvidenceKind.TextRichContainer,
            observation.TextContainment);

        Assert.Equal(
            VisualEvidenceKind.NativeTextContainerOrFrame,
            new DefaultVisualEvidenceAssessor()
                .Assess(
                    observation)
                .Kind);
    }

    [Fact]
    public void Enrich_LexicalCaptionLeadWord_ProducesStrongCaptionAssociationBeforeContainerPolicy()
    {
        var fixture =
            CaptionFixture();

        var observation =
            EnrichSingle(
                fixture,
                Measured(
                    effectiveBounds:
                        new NormalizedRectangle(
                            0.200,
                            0.300,
                            0.800,
                            0.500),
                    pixelInteraction:
                        VisualPixelInteractionKind.ForegroundWordInteraction,
                    foregroundPixelRatio:
                        0.032,
                    nativeWordsTouchedRatio:
                        0.05,
                    significantComponentCount:
                        5));

        Assert.Equal(
            CaptionAssociationEvidenceKind.StrongAssociation,
            observation.CaptionAssociation);

        Assert.Equal(
            VisualEvidenceKind.CaptionedMeaningfulVisual,
            new DefaultVisualEvidenceAssessor()
                .Assess(
                    observation)
                .Kind);
    }

    [Fact]
    public void Enrich_BlankCanvas_DoesNotInventStructuralEvidence()
    {
        var fixture =
            HeadingFixture();

        var observation =
            EnrichSingle(
                fixture,
                Blank());

        Assert.Equal(
            VisualForegroundState.BlankCanvas,
            observation.ForegroundState);

        Assert.Equal(
            HeadingAssociationEvidenceKind.NotMeasured,
            observation.HeadingAssociation);

        Assert.Equal(
            NativeTextContainmentEvidenceKind.NotMeasured,
            observation.TextContainment);

        Assert.Equal(
            CaptionAssociationEvidenceKind.NotMeasured,
            observation.CaptionAssociation);
    }

    [Fact]
    public void Enrich_UnavailableRaster_DoesNotInventNegativeStructuralEvidence()
    {
        var fixture =
            HeadingFixture();

        var observation =
            EnrichSingle(
                fixture,
                Unavailable());

        Assert.Equal(
            VisualForegroundState.Unavailable,
            observation.ForegroundState);

        Assert.Equal(
            HeadingAssociationEvidenceKind.NotMeasured,
            observation.HeadingAssociation);

        Assert.Equal(
            NativeTextContainmentEvidenceKind.NotMeasured,
            observation.TextContainment);

        Assert.Equal(
            CaptionAssociationEvidenceKind.NotMeasured,
            observation.CaptionAssociation);

        Assert.Equal(
            VisualEvidenceKind.Unknown,
            new DefaultVisualEvidenceAssessor()
                .Assess(
                    observation)
                .Kind);
    }

    [Fact]
    public void Enrich_OutOfPageEffectiveArea_FailsClosedToUnknown()
    {
        var fixture =
            HeadingFixture();

        var observation =
            EnrichSingle(
                fixture,
                Measured(
                    effectiveBounds:
                        new NormalizedRectangle(
                            -0.5,
                            -0.5,
                            1.5,
                            1.5),
                    pixelInteraction:
                        VisualPixelInteractionKind.NoForegroundWordIntersection,
                    foregroundPixelRatio:
                        0.10,
                    nativeWordsTouchedRatio:
                        0,
                    significantComponentCount:
                        3));

        Assert.Equal(
            VisualForegroundState.Unavailable,
            observation.ForegroundState);

        Assert.Equal(
            VisualEvidenceKind.Unknown,
            new DefaultVisualEvidenceAssessor()
                .Assess(
                    observation)
                .Kind);
    }

    [Fact]
    public void Enrich_RejectsNormalizationFromDifferentExtraction()
    {
        var fixture =
            HeadingFixture();

        var other =
            HeadingFixture();

        Assert.Throws<InvalidDataException>(
            () =>
                new DefaultVisualStructuralEvidenceEnricher()
                    .Enrich(
                        fixture.Extraction,
                        other.Normalization,
                        [
                            new PageVisualRasterObservations(
                                1,
                                [
                                    Blank()
                                ])
                        ]));
    }

    [Fact]
    public void Enrich_RejectsRasterObservationCountDrift()
    {
        var fixture =
            HeadingFixture();

        Assert.Throws<InvalidDataException>(
            () =>
                new DefaultVisualStructuralEvidenceEnricher()
                    .Enrich(
                        fixture.Extraction,
                        fixture.Normalization,
                        [
                            new PageVisualRasterObservations(
                                1,
                                [])
                        ]));
    }

    [Fact]
    public void Enrich_RejectsRasterSourceVisualOrderDrift()
    {
        var fixture =
            HeadingFixture();

        Assert.Throws<InvalidDataException>(
            () =>
                new DefaultVisualStructuralEvidenceEnricher()
                    .Enrich(
                        fixture.Extraction,
                        fixture.Normalization,
                        [
                            new PageVisualRasterObservations(
                                1,
                                [
                                    Blank(
                                        sourceVisualIndex:
                                            1)
                                ])
                        ]));
    }

    private static VisualEvidenceObservation EnrichSingle(
        Fixture fixture,
        VisualRasterObservation raster)
    {
        var pages =
            new DefaultVisualStructuralEvidenceEnricher()
                .Enrich(
                    fixture.Extraction,
                    fixture.Normalization,
                    [
                        new PageVisualRasterObservations(
                            1,
                            [
                                raster
                            ])
                    ]);

        return Assert.Single(
            Assert.Single(
                pages)
                .VisualElements);
    }

    private static Fixture HeadingFixture()
    {
        var sourceSequence =
            0;

        var wordSequence =
            0;

        var heading =
            Block(
                ref sourceSequence,
                ref wordSequence,
                "SECTION TITLE",
                new NormalizedRectangle(
                    0.300,
                    0.200,
                    0.700,
                    0.250),
                pointSize:
                    20);

        var body =
            Block(
                ref sourceSequence,
                ref wordSequence,
                "This body paragraph contains enough ordinary words to establish a stable ten point body font.",
                new NormalizedRectangle(
                    0.100,
                    0.400,
                    0.900,
                    0.620),
                pointSize:
                    10);

        return FixtureFromBlocks(
            [
                heading,
                body
            ],
            wordCount:
                heading.Words.Count +
                body.Words.Count);
    }

    private static Fixture TextRichFixture()
    {
        var sourceSequence =
            0;

        var wordSequence =
            0;

        var body =
            Block(
                ref sourceSequence,
                ref wordSequence,
                "This contained paragraph deliberately has more than twelve words so that the frozen paragraph like containment rule is exercised directly.",
                new NormalizedRectangle(
                    0.100,
                    0.400,
                    0.900,
                    0.600),
                pointSize:
                    10);

        return FixtureFromBlocks(
            [
                body
            ],
            wordCount:
                body.Words.Count);
    }

    private static Fixture CaptionFixture()
    {
        var sourceSequence =
            0;

        var wordSequence =
            0;

        var body =
            Block(
                ref sourceSequence,
                ref wordSequence,
                "Ordinary body text establishes the normal font size for this synthetic page.",
                new NormalizedRectangle(
                    0.100,
                    0.700,
                    0.900,
                    0.800),
                pointSize:
                    10);

        var caption =
            Block(
                ref sourceSequence,
                ref wordSequence,
                "Figure 3.2 Sample caption",
                new NormalizedRectangle(
                    0.250,
                    0.520,
                    0.750,
                    0.560),
                pointSize:
                    10);

        return FixtureFromBlocks(
            [
                body,
                caption
            ],
            wordCount:
                body.Words.Count +
                caption.Words.Count);
    }

    private static Fixture FixtureFromBlocks(
        IReadOnlyList<DocumentTextBlock> blocks,
        int wordCount)
    {
        var words =
            blocks
                .SelectMany(
                    block =>
                        block.Words)
                .OrderBy(
                    word =>
                        word.SourceSequence)
                .ToArray();

        var extractionPage =
            new DocumentExtractionPage(
                physicalPageNumber:
                    1,
                sourceText:
                    string.Join(
                        " ",
                        blocks.Select(
                            block =>
                                block.Text)),
                wordCount:
                    wordCount,
                rasterImageCount:
                    1,
                largestRasterImageAreaRatio:
                    0.67,
                words:
                    words,
                blocks:
                    blocks);

        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    extractionPage
                ]);

        var normalization =
            new DocumentTextNormalizer()
                .Normalize(
                    extraction);

        return new Fixture(
            extraction,
            normalization);
    }

    private static DocumentTextBlock Block(
        ref int sourceSequence,
        ref int wordSequence,
        string text,
        NormalizedRectangle bounds,
        double pointSize)
    {
        var tokens =
            text.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries);

        var width =
            Math.Max(
                0.0001,
                bounds.Right -
                bounds.Left);

        var words =
            new DocumentWord[
                tokens.Length];

        for (var index = 0;
             index <
             tokens.Length;
             index++)
        {
            var left =
                bounds.Left +
                width *
                index /
                tokens.Length;

            var right =
                bounds.Left +
                width *
                (
                    index +
                    1
                ) /
                tokens.Length;

            words[
                index] =
                new DocumentWord(
                    wordSequence++,
                    tokens[index],
                    new NormalizedRectangle(
                        left,
                        bounds.Top,
                        right,
                        bounds.Bottom),
                    medianPointSize:
                        pointSize);
        }

        return new DocumentTextBlock(
            sourceSequence:
                sourceSequence++,
            readingOrder:
                sourceSequence -
                1,
            text,
            bounds,
            words,
            medianPointSize:
                pointSize,
            lineCount:
                1);
    }

    private static VisualRasterObservation Measured(
        NormalizedRectangle effectiveBounds,
        VisualPixelInteractionKind pixelInteraction,
        double foregroundPixelRatio,
        double nativeWordsTouchedRatio,
        int significantComponentCount,
        int sourceVisualIndex = 0) =>
        new(
            sourceVisualIndex,
            declaredPageBounds:
                new NormalizedRectangle(
                    0,
                    0,
                    1,
                    1),
            VisualRasterDecodeSource.RawEmbeddedImage,
            pixelWidth:
                100,
            pixelHeight:
                100,
            backgroundUniformity:
                1,
            VisualForegroundState.Measured,
            foregroundPixelRatio,
            pixelInteraction,
            nativeWordsTouchedRatio,
            significantComponentCount,
            effectiveBounds);

    private static VisualRasterObservation Blank(
        int sourceVisualIndex = 0) =>
        new(
            sourceVisualIndex,
            declaredPageBounds:
                new NormalizedRectangle(
                    0,
                    0,
                    1,
                    1),
            VisualRasterDecodeSource.RawEmbeddedImage,
            pixelWidth:
                100,
            pixelHeight:
                100,
            backgroundUniformity:
                1,
            VisualForegroundState.BlankCanvas,
            foregroundPixelRatio:
                0,
            VisualPixelInteractionKind.BlankCanvas,
            nativeWordsTouchedRatio:
                0,
            significantComponentCount:
                0,
            effectiveVisualBounds:
                null);

    private static VisualRasterObservation Unavailable(
        int sourceVisualIndex = 0) =>
        new(
            sourceVisualIndex,
            declaredPageBounds:
                new NormalizedRectangle(
                    0,
                    0,
                    1,
                    1),
            VisualRasterDecodeSource.Unavailable,
            pixelWidth:
                null,
            pixelHeight:
                null,
            backgroundUniformity:
                null,
            VisualForegroundState.Unavailable,
            foregroundPixelRatio:
                null,
            VisualPixelInteractionKind.NotMeasured,
            nativeWordsTouchedRatio:
                0,
            significantComponentCount:
                null,
            effectiveVisualBounds:
                null);

    private sealed record Fixture(
        DocumentExtractionResult Extraction,
        DocumentProcessing.Core.Normalization.DocumentTextNormalizationResult Normalization);
}
