using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Provenance;
using DocumentProcessing.Engine.Orchestration;
using DocumentProcessing.Pdf;
using DocumentProcessing.Formats.Pdf;

namespace DocumentProcessing.UnitTests.Orchestration;

/// <summary>
/// Proves the first real format strategy through the consumer-facing host.
/// </summary>
public sealed class DocumentProcessingHostPdfRoutingTests
{
    #region Variables and Constants

    private static readonly ProcessingComponentIdentity NativeIdentity =
        new(
            "fake-native",
            "fake-native-v1");

    #endregion

    #region Methods Tests

    [Fact]
    public async Task ProcessDocumentAsync_Pdf_ExecutesThroughHostAndPdfStrategy()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    NativePage(
                        "Native PDF text.")
                ]);

        var detector =
            new PdfDocumentTypeDetector();

        var authoritativePdfProcessor =
            new DocumentProcessor(
                detector,
                new StubExtractor(
                    extraction),
                new PdfPreflightAnalyzer(),
                "test-engine-v1",
                NativeIdentity);

        var pdfStrategy =
            new PdfDocumentFormatProcessor(
                authoritativePdfProcessor);

        var host =
            new global::DocumentProcessing.DocumentProcessingHost(
                detector,
                [pdfStrategy]);

        await using var stream =
            new MemoryStream(
                "%PDF-host-strategy-test"u8.ToArray(),
                writable:
                    false);

        var result =
            await host.ProcessDocumentAsync(
                new DocumentSource(
                    stream,
                    "fixture.pdf",
                    "application/pdf"));

        Assert.Equal(
            DocumentFormatId.Pdf,
            result.Source.Format);

        Assert.Single(
            result.Pages);

        var element =
            Assert.Single(
                result.Elements);

        Assert.Equal(
            "Native PDF text.",
            element.NormalizedText);
    }

    [Fact]
    public void PdfDocumentFormatProcessor_DeclaresPdfFormat()
    {
        var extraction =
            new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                [
                    NativePage(
                        "Native PDF text.")
                ]);

        var processor =
            new PdfDocumentFormatProcessor(
                new DocumentProcessor(
                    new PdfDocumentTypeDetector(),
                    new StubExtractor(
                        extraction),
                    new PdfPreflightAnalyzer(),
                    "test-engine-v1",
                    NativeIdentity));

        Assert.Equal(
            DocumentFormatId.Pdf,
            processor.Format);
    }

    #endregion

    #region Methods Fixtures

    private static DocumentExtractionPage NativePage(
        string text)
    {
        var word =
            new DocumentWord(
                sourceSequence:
                    0,
                text,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.40,
                    0.15),
                fontName:
                    "Body",
                medianPointSize:
                    10);

        var block =
            new DocumentTextBlock(
                sourceSequence:
                    0,
                readingOrder:
                    0,
                text,
                new NormalizedRectangle(
                    0.10,
                    0.10,
                    0.60,
                    0.20),
                words:
                    [word],
                dominantFontName:
                    "Body",
                medianPointSize:
                    10,
                lineCount:
                    1);

        return new DocumentExtractionPage(
            physicalPageNumber:
                1,
            text,
            new NormalizedRectangle(
                0,
                0,
                1,
                1),
            wordCount:
                1,
            rasterImageCount:
                0,
            largestRasterImageAreaRatio:
                0,
            sourceWidth:
                1000,
            sourceHeight:
                1000,
            words:
                [word],
            blocks:
                [block]);
    }

    #endregion

    #region Test Types

    private sealed class StubExtractor(
        DocumentExtractionResult extraction)
        : IDocumentExtractor
    {
        public bool CanExtract(
            DocumentFormatId format) =>
            format ==
            DocumentFormatId.Pdf;

        public ValueTask<DocumentExtractionResult> ExtractAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(
                source);

            cancellationToken.ThrowIfCancellationRequested();

            if (!CanExtract(
                    format))
            {
                throw new NotSupportedException(
                    $"Test extractor cannot process '{format}'.");
            }

            return ValueTask.FromResult(
                extraction);
        }
    }

    #endregion
}
