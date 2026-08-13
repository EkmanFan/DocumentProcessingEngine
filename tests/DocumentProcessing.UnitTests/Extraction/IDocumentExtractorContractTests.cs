using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;

namespace DocumentProcessing.UnitTests.Extraction;

public sealed class IDocumentExtractorContractTests
{
    [Fact]
    public async Task Extractor_CanAdvertiseAndProcessFutureFormatWithoutChangingCoreContract()
    {
        var format = new DocumentFormatId("epub");
        IDocumentExtractor extractor = new StubDocumentExtractor(format);
        await using var stream = new MemoryStream([1, 2, 3]);
        var source = new DocumentSource(stream, "sample.epub", "application/epub+zip");

        Assert.True(extractor.CanExtract(format));

        var result = await extractor.ExtractAsync(source, format);

        Assert.Equal(format, result.Format);
    }

    private sealed class StubDocumentExtractor(DocumentFormatId supportedFormat) : IDocumentExtractor
    {
        public bool CanExtract(DocumentFormatId format) => format == supportedFormat;

        public ValueTask<DocumentExtractionResult> ExtractAsync(
            DocumentSource source,
            DocumentFormatId format,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);

            if (!CanExtract(format))
            {
                throw new NotSupportedException($"Format '{format}' is not supported.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(new DocumentExtractionResult(format));
        }
    }
}
