using DocumentProcessing.Core.Documents;
using DocumentProcessing.Pdf;
using UglyToad.PdfPig.Writer;

namespace DocumentProcessing.UnitTests.Orchestration;

public sealed class PdfPhysicalPageRangeTests
{
    #region Tests

    [Fact]
    public async Task Format_ExtractsOnlyRequestedOriginalPhysicalPages()
    {
        await using var stream =
            new MemoryStream(
                BuildThreePagePdf(),
                writable:
                    false);

        var outcome =
            await new PdfDocumentFormat()
                .TryExtractNativeEvidenceAsync(
                    new DocumentSource(
                        stream,
                        "three-pages.pdf",
                        "application/pdf"),
                    new PhysicalPageRange(2, 2));

        var success =
            Assert.IsType<NativeEvidenceExtractionResult.Success>(
                outcome);

        var evidence =
            Assert.IsType<PagedNativeDocumentEvidence>(
                success.Evidence);

        Assert.Equal(
            2,
            Assert.Single(
                    evidence.Extraction.Pages)
                .PhysicalPageNumber);
    }

    [Fact]
    public async Task Format_RejectsRangePastDocumentBounds()
    {
        await using var stream =
            new MemoryStream(
                BuildThreePagePdf(),
                writable:
                    false);

        var outcome =
            await new PdfDocumentFormat()
                .TryExtractNativeEvidenceAsync(
                    new DocumentSource(
                        stream,
                        "three-pages.pdf",
                        "application/pdf"),
                    new PhysicalPageRange(2, 4));

        var invalid =
            Assert.IsType<NativeEvidenceExtractionResult.Invalid>(
                outcome);

        Assert.Contains(
            "exceeds",
            invalid.Reason,
            StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Test Fixtures

    private static byte[] BuildThreePagePdf()
    {
        var builder =
            new PdfDocumentBuilder();

        builder.AddPage(200, 200);
        builder.AddPage(200, 200);
        builder.AddPage(200, 200);

        return builder.Build();
    }

    #endregion
}
