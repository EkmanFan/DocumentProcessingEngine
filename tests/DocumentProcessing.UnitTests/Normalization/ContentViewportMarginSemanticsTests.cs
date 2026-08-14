using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Hybrid;
using DocumentProcessing.Core.Normalization;
using DocumentProcessing.Engine.Hybrid;
using DocumentProcessing.Engine.Hybrid.Normalization;
using DocumentProcessing.Engine.Normalization;

namespace DocumentProcessing.UnitTests.Normalization;

public sealed class ContentViewportMarginSemanticsTests
{
    [Fact]
    public void LegacyNormalizer_ClassifiesRecurringHeaderRelativeToShiftedContentViewport()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                Enumerable
                    .Range(
                        1,
                        3)
                    .Select(
                        pageNumber =>
                            Page(
                                pageNumber,
                                $"CHAPTER {pageNumber}",
                                HeaderInsideShiftedViewport))
                    .ToArray());

        var result =
            new DocumentTextNormalizer()
                .Normalize(
                    extraction);

        Assert.All(
            result.Pages
                .SelectMany(
                    page =>
                        page.Blocks),
            block =>
            {
                Assert.True(
                    block.IsExcluded);

                Assert.Equal(
                    DocumentBlockExclusionReason.RepeatedHeader,
                    block.ExclusionReason);
            });
    }

    [Fact]
    public void HybridNormalizer_ClassifiesRecurringHeaderRelativeToShiftedContentViewport()
    {
        var pages =
            Enumerable
                .Range(
                    1,
                    3)
                .Select(
                    pageNumber =>
                    {
                        var sourcePage =
                            Page(
                                pageNumber,
                                $"CHAPTER {pageNumber}",
                                HeaderInsideShiftedViewport);

                        var element =
                            HybridDocumentElementFactory
                                .FromNative(
                                    pageNumber,
                                    sourcePage.Blocks[0]);

                        return HybridDocumentAssembler
                            .AssemblePage(
                                sourcePage,
                                new[]
                                {
                                    element
                                });
                    })
                .ToArray();

        var assembly =
            HybridDocumentAssembler
                .AssembleDocument(
                    pages);

        var result =
            new HybridDocumentNormalizer()
                .Normalize(
                    assembly);

        Assert.All(
            result.Pages
                .SelectMany(
                    page =>
                        page.Elements),
            element =>
            {
                Assert.True(
                    element.IsExcluded);

                Assert.Equal(
                    DocumentBlockExclusionReason.RepeatedHeader,
                    element.ExclusionReason);
            });
    }

    [Fact]
    public void ShiftedContentViewport_DoesNotTurnRepeatedBodyTextIntoMargin()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                Enumerable
                    .Range(
                        1,
                        3)
                    .Select(
                        pageNumber =>
                            Page(
                                pageNumber,
                                "Repeated body",
                                BodyInsideShiftedViewport))
                    .ToArray());

        var result =
            new DocumentTextNormalizer()
                .Normalize(
                    extraction);

        Assert.All(
            result.Pages
                .SelectMany(
                    page =>
                        page.Blocks),
            block =>
                Assert.False(
                    block.IsExcluded));
    }

    [Fact]
    public void DefaultExtractionAndHybridPageViewport_RemainsFullCanonicalPage()
    {
        var extractionPage =
            new DocumentExtractionPage(
                1,
                "text");

        Assert.Equal(
            FullPageViewport,
            extractionPage.ContentViewport);

        var hybridPage =
            HybridDocumentAssembler
                .AssemblePage(
                    1,
                    new[]
                    {
                        HybridDocumentElementFactory
                            .FromNative(
                                1,
                                Block(
                                    "text",
                                    new NormalizedRectangle(
                                        0.1,
                                        0.2,
                                        0.9,
                                        0.3)))
                    });

        Assert.Equal(
            FullPageViewport,
            hybridPage.ContentViewport);
    }

    private static DocumentExtractionPage Page(
        int physicalPageNumber,
        string text,
        NormalizedRectangle bounds)
    {
        var block =
            Block(
                text,
                bounds);

        return new DocumentExtractionPage(
            physicalPageNumber,
            text,
            ShiftedContentViewport,
            wordCount:
                1,
            sourceWidth:
                600,
            sourceHeight:
                800,
            blocks:
                new[]
                {
                    block
                });
    }

    private static DocumentTextBlock Block(
        string text,
        NormalizedRectangle bounds) =>
        new(
            sourceSequence:
                0,
            readingOrder:
                0,
            text,
            bounds);

    private static readonly NormalizedRectangle FullPageViewport =
        new(
            0,
            0,
            1,
            1);

    private static readonly NormalizedRectangle ShiftedContentViewport =
        new(
            0.10,
            0.18,
            0.90,
            0.95);

    private static readonly NormalizedRectangle HeaderInsideShiftedViewport =
        new(
            0.20,
            0.2031,
            0.80,
            0.2416);

    private static readonly NormalizedRectangle BodyInsideShiftedViewport =
        new(
            0.20,
            0.4495,
            0.80,
            0.5265);
}
