using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocumentProcessing.Pdf;

public sealed class PdfPigDocumentExtractor : IDocumentExtractor
{
    public bool CanExtract(DocumentFormatId format) => format == DocumentFormatId.Pdf;

    public async ValueTask<DocumentExtractionResult> ExtractAsync(
        DocumentSource source,
        DocumentFormatId format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!CanExtract(format))
        {
            throw new NotSupportedException($"Format '{format}' is not supported by the PDF extractor.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var input = source.Content;
        MemoryStream? bufferedInput = null;
        long? originalPosition = null;

        try
        {
            if (input.CanSeek)
            {
                originalPosition = input.Position;
                input.Position = 0;
            }
            else
            {
                bufferedInput = new MemoryStream();
                await input.CopyToAsync(bufferedInput, cancellationToken).ConfigureAwait(false);
                bufferedInput.Position = 0;
                input = bufferedInput;
            }

            using var document = PdfDocument.Open(input);
            var pages = new List<DocumentExtractionPage>(document.NumberOfPages);
            var physicalPageNumber = 0;

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                physicalPageNumber++;
                pages.Add(new DocumentExtractionPage(
                    physicalPageNumber,
                    ContentOrderTextExtractor.GetText(page)));
            }

            return new DocumentExtractionResult(DocumentFormatId.Pdf, pages);
        }
        finally
        {
            bufferedInput?.Dispose();
            if (originalPosition.HasValue)
            {
                source.Content.Position = originalPosition.Value;
            }
        }
    }
}
