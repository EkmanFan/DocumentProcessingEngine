using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

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

                var sourceWidth = Convert.ToDouble(page.Width);
                var sourceHeight = Convert.ToDouble(page.Height);

                if (sourceWidth <= 0 || sourceHeight <= 0)
                {
                    throw new InvalidDataException(
                        $"PDF page {physicalPageNumber} has invalid dimensions " +
                        $"{sourceWidth} x {sourceHeight}.");
                }

                var sourceWords = page.GetWords(NearestNeighbourWordExtractor.Instance)
                    .Where(word => !string.IsNullOrWhiteSpace(word.Text))
                    .ToArray();

                var documentWords = sourceWords
                    .Select((word, sourceSequence) =>
                        ToDocumentWord(
                            word,
                            sourceSequence,
                            sourceWidth,
                            sourceHeight))
                    .ToArray();

                var documentWordBySourceWord =
                    new Dictionary<Word, DocumentWord>(ReferenceEqualityComparer.Instance);

                for (var i = 0; i < sourceWords.Length; i++)
                {
                    documentWordBySourceWord.Add(sourceWords[i], documentWords[i]);
                }

                var sourceBlocks = DocstrumBoundingBoxes.Instance
                    .GetBlocks(sourceWords);

                var blockSourceSequence =
                    new Dictionary<TextBlock, int>(ReferenceEqualityComparer.Instance);

                for (var i = 0; i < sourceBlocks.Count; i++)
                {
                    blockSourceSequence.Add(sourceBlocks[i], i);
                }

                var orderedBlocks = UnsupervisedReadingOrderDetector.Instance
                    .Get(sourceBlocks)
                    .ToArray();

                var documentBlocks = orderedBlocks
                    .Select(block =>
                        ToDocumentTextBlock(
                            block,
                            blockSourceSequence[block],
                            documentWordBySourceWord,
                            sourceWidth,
                            sourceHeight))
                    .ToArray();

                var images = page.GetImages().ToArray();
                var pageArea = sourceWidth * sourceHeight;

                var largestRasterImageAreaRatio = images
                    .Select(image =>
                        Convert.ToDouble(image.BoundingBox.Width) *
                        Convert.ToDouble(image.BoundingBox.Height) /
                        pageArea)
                    .DefaultIfEmpty(0)
                    .Max();

                pages.Add(new DocumentExtractionPage(
                    physicalPageNumber,
                    ContentOrderTextExtractor.GetText(page),
                    documentWords.Length,
                    images.Length,
                    largestRasterImageAreaRatio,
                    sourceWidth,
                    sourceHeight,
                    documentWords,
                    documentBlocks));
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

    private static DocumentWord ToDocumentWord(
        Word word,
        int sourceSequence,
        double pageWidth,
        double pageHeight) =>
        new(
            sourceSequence,
            word.Text,
            ToNormalizedRectangle(word.BoundingBox, pageWidth, pageHeight));

    private static DocumentTextBlock ToDocumentTextBlock(
        TextBlock block,
        int sourceSequence,
        IReadOnlyDictionary<Word, DocumentWord> documentWordBySourceWord,
        double pageWidth,
        double pageHeight)
    {
        var words = block.TextLines
            .SelectMany(line => line.Words)
            .Select(word =>
                documentWordBySourceWord.TryGetValue(word, out var documentWord)
                    ? documentWord
                    : throw new InvalidDataException(
                        "PdfPig layout analysis returned a word that was not present " +
                        "in the native word extraction result."))
            .ToArray();

        return new DocumentTextBlock(
            sourceSequence,
            block.ReadingOrder >= 0 ? block.ReadingOrder : null,
            block.Text,
            ToNormalizedRectangle(block.BoundingBox, pageWidth, pageHeight),
            words);
    }

    private static NormalizedRectangle ToNormalizedRectangle(
        PdfRectangle bounds,
        double pageWidth,
        double pageHeight)
    {
        var left = Convert.ToDouble(bounds.Left) / pageWidth;
        var right = Convert.ToDouble(bounds.Right) / pageWidth;

        // PdfPig uses a bottom-left origin. Core uses a top-left origin.
        var top = 1 - Convert.ToDouble(bounds.Top) / pageHeight;
        var bottom = 1 - Convert.ToDouble(bounds.Bottom) / pageHeight;

        return new NormalizedRectangle(left, top, right, bottom);
    }
}
