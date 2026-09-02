using System.Text;
using System.Text.Json;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Outline.Destinations;
using UglyToad.PdfPig.Writer;

namespace DocumentProcessing.IntegrationTests.Pdf;

public sealed class PdfDocumentFormatTests
{
    #region Methods Tests

    [Fact]
    public void Format_IsPdf()
    {
        IDocumentFormat format =
            new PdfDocumentFormat();

        Assert.Equal(
            DocumentFormatId.Pdf,
            format.Format);
    }

    [Fact]
    public async Task TryExtractNativeEvidenceAsync_NotRecognizedForNonPdfAndRestoresPosition()
    {
        await using var stream =
            new MemoryStream(
                Encoding.ASCII.GetBytes(
                    "not a pdf"));

        stream.Position =
            2;

        var source =
            new DocumentSource(
                stream,
                "misleading.pdf",
                "application/pdf");

        var result =
            await new PdfDocumentFormat()
                .TryExtractNativeEvidenceAsync(
                    source);

        Assert.IsType<
            NativeEvidenceExtractionResult.NotRecognized>(
            result);

        Assert.Equal(
            2,
            stream.Position);

        Assert.True(
            stream.CanRead);
    }

    [Fact]
    public async Task TryExtractNativeEvidenceAsync_SuccessPreservesExactCurrentCoordinatedEvidence()
    {
        var pdfBytes =
            CreateSinglePagePdf(
                "Native evidence parity");

        await using var baselineStream =
            new MemoryStream(
                pdfBytes);

        var baselineSource =
            new DocumentSource(
                baselineStream,
                "fixture.pdf",
                "application/pdf");

        var baseline =
            await new PdfPigDocumentExtractor()
                .ExtractWithRasterObservationsAsync(
                    baselineSource,
                    DocumentFormatId.Pdf,
                    new PdfPigVisualRasterObservationSource());

        await using var candidateStream =
            new MemoryStream(
                pdfBytes);

        candidateStream.Position =
            7;

        var candidateSource =
            new DocumentSource(
                candidateStream,
                "fixture.pdf",
                "application/pdf");

        var outcome =
            await new PdfDocumentFormat()
                .TryExtractNativeEvidenceAsync(
                    candidateSource);

        var success =
            Assert.IsType<
                NativeEvidenceExtractionResult.Success>(
                outcome);

        Assert.Equal(
            DocumentFormatId.Pdf,
            Assert.IsType<PagedNativeDocumentEvidence>(success.Evidence).Extraction.Format);

        Assert.Equal(
            Serialize(
                baseline.Extraction),
            Serialize(
                Assert.IsType<PagedNativeDocumentEvidence>(success.Evidence).Extraction));

        Assert.Equal(
            Serialize(
                baseline.RasterObservations),
            Serialize(
                Assert.IsType<PagedNativeDocumentEvidence>(success.Evidence).RasterObservations));

        Assert.Equal(
            Serialize(
                baseline.RasterObservationFailure),
            Serialize(
                Assert.IsType<PagedNativeDocumentEvidence>(success.Evidence).RasterObservationFailure));

        Assert.Equal(
            7,
            candidateStream.Position);

        Assert.True(
            candidateStream.CanRead);
    }

    [Fact]
    public async Task TryExtractNativeEvidenceAsync_RecognizedMalformedPdfReturnsInvalid()
    {
        var validPdf =
            CreateSinglePagePdf(
                "Will be truncated");

        var malformedPdf =
            validPdf[
                ..Math.Min(
                    32,
                    validPdf.Length)];

        Assert.True(
            malformedPdf
                .AsSpan()
                .StartsWith(
                    "%PDF-"u8));

        await using var stream =
            new MemoryStream(
                malformedPdf);

        var result =
            await new PdfDocumentFormat()
                .TryExtractNativeEvidenceAsync(
                    new DocumentSource(
                        stream,
                        "truncated.pdf",
                        "application/pdf"));

        var invalid =
            Assert.IsType<
                NativeEvidenceExtractionResult.Invalid>(
                result);

        Assert.False(
            string.IsNullOrWhiteSpace(
                invalid.Reason));
    }

    [Fact]
    public async Task TryExtractNativeEvidenceAsync_PropagatesCallerCancellation()
    {
        await using var stream =
            new MemoryStream(
                Encoding.ASCII.GetBytes(
                    "%PDF-1.7\nfixture"));

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<
            OperationCanceledException>(
            async () =>
                await new PdfDocumentFormat()
                    .TryExtractNativeEvidenceAsync(
                        new DocumentSource(
                            stream),
                        cancellation.Token));
    }

    [Fact]
    public async Task TryInspectNativeNavigationAsync_MapsInternalBookmarksInPublisherOrder()
    {
        await using var stream =
            new MemoryStream(
                CreatePdfWithBookmarks());

        stream.Position =
            7;

        var inspection =
            await new PdfDocumentFormat()
                .TryInspectNativeNavigationAsync(
                    new DocumentSource(
                        stream,
                        "outlined.pdf",
                        "application/pdf"));

        Assert.NotNull(
            inspection);

        Assert.Equal(
            DocumentFormatId.Pdf,
            inspection.Format);

        Assert.Equal(
            4,
            Assert.IsType<DocumentStructureAxis.PhysicalPages>(
                    inspection.Axis)
                .PhysicalPageCount);

        Assert.Collection(
            inspection.Entries,
            entry =>
                AssertNavigationEntry(
                    entry,
                    "Chapter 1",
                    hierarchyLevel:
                        0,
                    sourceOrder:
                        0,
                    physicalPageNumber:
                        1),
            entry =>
                AssertNavigationEntry(
                    entry,
                    "Section 1.1",
                    hierarchyLevel:
                        1,
                    sourceOrder:
                        1,
                    physicalPageNumber:
                        2),
            entry =>
                AssertNavigationEntry(
                    entry,
                    "Chapter 2",
                    hierarchyLevel:
                        0,
                    sourceOrder:
                        2,
                    physicalPageNumber:
                        3));

        Assert.Equal(
            7,
            stream.Position);
    }

    [Fact]
    public async Task TryInspectStructuralHeadingsAsync_UsesNativeTypographyAndIgnoresRunningHeaders()
    {
        await using var stream =
            new MemoryStream(
                CreatePdfWithStructuralHeadings());

        stream.Position =
            7;

        var format =
            new PdfDocumentFormat();

        var navigation =
            await format.TryInspectNativeNavigationAsync(
                new DocumentSource(
                    stream,
                    "headings.pdf",
                    "application/pdf"));

        var inspection =
            await format.TryInspectStructuralHeadingsAsync(
                new DocumentSource(
                    stream,
                    "headings.pdf",
                    "application/pdf"));

        Assert.NotNull(
            navigation);

        Assert.Empty(
            navigation.Entries);

        Assert.NotNull(
            inspection);

        Assert.Equal(
            5,
            Assert.IsType<DocumentStructureAxis.PhysicalPages>(
                    inspection.Axis)
                .PhysicalPageCount);

        Assert.Collection(
            inspection.Entries,
            entry =>
                AssertStructuralHeadingEntry(
                    entry,
                    "Chapter One",
                    hierarchyLevel:
                        0,
                    physicalPageNumber:
                        2),
            entry =>
                AssertStructuralHeadingEntry(
                    entry,
                    "Chapter Two",
                    hierarchyLevel:
                        0,
                    physicalPageNumber:
                        4));

        Assert.Equal(
            7,
            stream.Position);
    }

    #endregion

    #region Methods Fixtures

    private static byte[] CreateSinglePagePdf(
        string text)
    {
        var builder =
            new PdfDocumentBuilder();

        var font =
            builder.AddStandard14Font(
                Standard14Font.Helvetica);

        var page =
            builder.AddPage(
                PageSize.A4);

        page.AddText(
            text,
            12,
            new PdfPoint(
                72,
                720),
            font);

        return builder.Build();
    }

    private static byte[] CreatePdfWithBookmarks()
    {
        var builder =
            new PdfDocumentBuilder();

        var font =
            builder.AddStandard14Font(
                Standard14Font.Helvetica);

        for (var pageNumber = 1;
             pageNumber <= 4;
             pageNumber++)
        {
            builder
                .AddPage(
                    PageSize.A4)
                .AddText(
                    $"Page {pageNumber}",
                    12,
                    new PdfPoint(
                        72,
                        720),
                    font);
        }

        builder.Bookmarks =
            new Bookmarks(
                [
                    new DocumentBookmarkNode(
                        "Chapter 1",
                        0,
                        Destination(
                            1),
                        [
                            new DocumentBookmarkNode(
                                "Section 1.1",
                                1,
                                Destination(
                                    2),
                                [])
                        ]),
                    new DocumentBookmarkNode(
                        "Chapter 2",
                        0,
                        Destination(
                            3),
                        [])
                ]);

        return builder.Build();
    }

    private static byte[] CreatePdfWithStructuralHeadings()
    {
        var builder =
            new PdfDocumentBuilder();

        var font =
            builder.AddStandard14Font(
                Standard14Font.Helvetica);

        for (var pageNumber = 1;
             pageNumber <= 5;
             pageNumber++)
        {
            var page =
                builder.AddPage(
                    PageSize.A4);

            page.AddText(
                "Example Book",
                16,
                new PdfPoint(
                    72,
                    790),
                font);

            if (pageNumber is 2 or 4)
            {
                page.AddText(
                    pageNumber ==
                        2
                        ? "Chapter One"
                        : "Chapter Two",
                    24,
                    new PdfPoint(
                        72,
                        700),
                    font);
            }

            page.AddText(
                "This ordinary paragraph contains enough native body words to establish the document baseline.",
                12,
                new PdfPoint(
                    72,
                    620),
                font);
        }

        return builder.Build();
    }

    private static ExplicitDestination Destination(
        int physicalPageNumber) =>
        new(
            physicalPageNumber,
            ExplicitDestinationType.FitPage,
            ExplicitDestinationCoordinates.Empty);

    private static void AssertNavigationEntry(
        NativeDocumentNavigationEntry entry,
        string expectedTitle,
        int hierarchyLevel,
        int sourceOrder,
        int physicalPageNumber)
    {
        Assert.Equal(
            expectedTitle,
            entry.Title);

        Assert.Equal(
            hierarchyLevel,
            entry.HierarchyLevel);

        Assert.Equal(
            sourceOrder,
            entry.SourceOrder);

        Assert.Equal(
            physicalPageNumber,
            Assert.IsType<DocumentStructurePosition.PhysicalPage>(
                    entry.Position)
                .PhysicalPageNumber);
    }

    private static void AssertStructuralHeadingEntry(
        StructuralHeadingEntry entry,
        string expectedTitle,
        int hierarchyLevel,
        int physicalPageNumber)
    {
        Assert.Equal(
            expectedTitle,
            entry.Title);

        Assert.Equal(
            hierarchyLevel,
            entry.HierarchyLevel);

        Assert.Equal(
            physicalPageNumber,
            Assert.IsType<DocumentStructurePosition.PhysicalPage>(
                    entry.Position)
                .PhysicalPageNumber);
    }

    private static string Serialize<T>(
        T value) =>
        JsonSerializer.Serialize(
            value);

    #endregion
}
