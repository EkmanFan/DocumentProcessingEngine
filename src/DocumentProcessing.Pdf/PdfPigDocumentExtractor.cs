using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace DocumentProcessing.Pdf;

public sealed class PdfPigDocumentExtractor
    : IDocumentExtractorWithRasterObservations
{
    // PdfPig 0.1.15 appends orientation buckets to a shared result list in parallel.
    // SourceSequence is provenance, so this stage must preserve deterministic bucket order.
    private static readonly NearestNeighbourWordExtractor DeterministicWordExtractor =
        new(
            new NearestNeighbourWordExtractor
                .NearestNeighbourWordExtractorOptions
            {
                MaxDegreeOfParallelism =
                    1,
                GroupByOrientation =
                    true
            });

    public bool CanExtract(
        DocumentFormatId format) =>
        format ==
        DocumentFormatId.Pdf;

    public async ValueTask<DocumentExtractionResult> ExtractAsync(
        DocumentSource source,
        DocumentFormatId format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!CanExtract(format))
        {
            throw new NotSupportedException(
                $"Format '{format}' is not supported by the PDF extractor.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var input =
            source.Content;

        MemoryStream? bufferedInput = null;
        long? originalPosition = null;

        try
        {
            if (input.CanSeek)
            {
                originalPosition =
                    input.Position;

                input.Position = 0;
            }
            else
            {
                bufferedInput =
                    new MemoryStream();

                await input
                    .CopyToAsync(
                        bufferedInput,
                        cancellationToken)
                    .ConfigureAwait(false);

                bufferedInput.Position = 0;
                input = bufferedInput;
            }

            using var document =
                PdfDocument.Open(input);

            var pages =
                new List<DocumentExtractionPage>(
                    document.NumberOfPages);

            var physicalPageNumber = 0;

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                physicalPageNumber++;

                pages.Add(
                    ExtractPage(
                        page,
                        physicalPageNumber,
                        out _,
                        out _));
            }

            return new DocumentExtractionResult(
                DocumentFormatId.Pdf,
                pages);
        }
        finally
        {
            bufferedInput?.Dispose();

            if (originalPosition.HasValue)
            {
                source.Content.Position =
                    originalPosition.Value;
            }
        }
    }

    public bool CanExtractWithRasterObservations(
        DocumentFormatId format,
        IVisualRasterObservationSource rasterObservationSource)
    {
        ArgumentNullException.ThrowIfNull(
            rasterObservationSource);

        return CanExtract(
                   format) &&
               rasterObservationSource is
                   PdfPigVisualRasterObservationSource;
    }

    public async ValueTask<DocumentExtractionWithRasterObservationsResult>
        ExtractWithRasterObservationsAsync(
            DocumentSource source,
            DocumentFormatId format,
            IVisualRasterObservationSource rasterObservationSource,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            rasterObservationSource);

        if (!CanExtractWithRasterObservations(
                format,
                rasterObservationSource))
        {
            throw new NotSupportedException(
                $"The configured PDF extractor and raster-observation source " +
                $"cannot coordinate format '{format}'.");
        }

        var pdfRasterObservationSource =
            (PdfPigVisualRasterObservationSource)
            rasterObservationSource;

        cancellationToken.ThrowIfCancellationRequested();

        var input =
            source.Content;

        MemoryStream? bufferedInput =
            null;

        long? originalPosition =
            null;

        try
        {
            if (input.CanSeek)
            {
                originalPosition =
                    input.Position;

                input.Position =
                    0;
            }
            else
            {
                bufferedInput =
                    new MemoryStream();

                await input
                    .CopyToAsync(
                        bufferedInput,
                        cancellationToken)
                    .ConfigureAwait(false);

                bufferedInput.Position =
                    0;

                input =
                    bufferedInput;
            }

            using var document =
                PdfDocument.Open(
                    input);

            var pages =
                new List<DocumentExtractionPage>(
                    document.NumberOfPages);

            var rasterObservations =
                new List<PageVisualRasterObservations>(
                    document.NumberOfPages);

            RasterObservationAcquisitionFailure?
                rasterObservationFailure =
                    null;

            var physicalPageNumber =
                0;

            foreach (var page in
                     document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                physicalPageNumber++;

                var extractionPage =
                    ExtractPage(
                        page,
                        physicalPageNumber,
                        out var coordinateSpace,
                        out var images);

                pages.Add(
                    extractionPage);

                if (rasterObservationFailure is not null)
                {
                    continue;
                }

                try
                {
                    rasterObservations.Add(
                        pdfRasterObservationSource
                            .ObservePage(
                                physicalPageNumber,
                                coordinateSpace,
                                images,
                                extractionPage,
                                cancellationToken));
                }
                catch (OperationCanceledException)
                    when (cancellationToken
                        .IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                    when (exception is not
                          OutOfMemoryException)
                {
                    rasterObservationFailure =
                        new RasterObservationAcquisitionFailure(
                            exception.GetType().FullName ??
                            exception.GetType().Name,
                            exception.Message);

                    // Partial raster-observation evidence must never masquerade as
                    // complete document coverage.
                    rasterObservations.Clear();
                }
            }

            var extraction =
                new DocumentExtractionResult(
                    DocumentFormatId.Pdf,
                    pages);

            return new DocumentExtractionWithRasterObservationsResult(
                extraction,
                rasterObservationFailure is null
                    ? rasterObservations
                    : null,
                rasterObservationFailure);
        }
        finally
        {
            bufferedInput?.Dispose();

            if (originalPosition.HasValue)
            {
                source.Content.Position =
                    originalPosition.Value;
            }
        }
    }

    internal static DocumentExtractionPage ExtractPage(
        Page page,
        int physicalPageNumber,
        out PdfPageCoordinateSpace coordinateSpace,
        out IPdfImage[] images)
    {
        ArgumentNullException.ThrowIfNull(
            page);

        coordinateSpace =
            PdfPageCoordinateSpace.Create(
                page);

        var resolvedCoordinateSpace =
            coordinateSpace;

        var sourceWidth =
            resolvedCoordinateSpace.Width;

        var sourceHeight =
            resolvedCoordinateSpace.Height;

        if (sourceWidth <= 0 ||
            sourceHeight <= 0)
        {
            throw new InvalidDataException(
                $"PDF page {physicalPageNumber} has invalid dimensions " +
                $"{sourceWidth} x {sourceHeight}.");
        }

        var sourceWords =
            page
                .GetWords(
                    DeterministicWordExtractor)
                .Where(word =>
                    !string.IsNullOrWhiteSpace(
                        word.Text))
                .ToArray();

        var documentWords =
            sourceWords
                .Select(
                    (word, sourceSequence) =>
                        ToDocumentWord(
                            word,
                            sourceSequence,
                            resolvedCoordinateSpace))
                .ToArray();

        var documentWordBySourceWord =
            new Dictionary<
                Word,
                DocumentWord>(
                ReferenceEqualityComparer.Instance);

        for (var index = 0;
             index < sourceWords.Length;
             index++)
        {
            documentWordBySourceWord.Add(
                sourceWords[index],
                documentWords[index]);
        }

        var sourceBlocks =
            DocstrumBoundingBoxes.Instance
                .GetBlocks(
                    sourceWords);

        var blockSourceSequence =
            new Dictionary<
                TextBlock,
                int>(
                ReferenceEqualityComparer.Instance);

        for (var index = 0;
             index < sourceBlocks.Count;
             index++)
        {
            blockSourceSequence.Add(
                sourceBlocks[index],
                index);
        }

        var orderedBlocks =
            UnsupervisedReadingOrderDetector.Instance
                .Get(
                    sourceBlocks)
                .ToArray();

        var documentBlocks =
            orderedBlocks
                .Select(block =>
                    ToDocumentTextBlock(
                        block,
                        blockSourceSequence[block],
                        documentWordBySourceWord,
                        resolvedCoordinateSpace))
                .ToArray();

        images =
            page.GetImages()
                .ToArray();

        var pageArea =
            sourceWidth *
            sourceHeight;

        var largestRasterImageAreaRatio =
            images
                .Select(image =>
                    Convert.ToDouble(
                        image.BoundingBox.Width) *
                    Convert.ToDouble(
                        image.BoundingBox.Height) /
                    pageArea)
                .DefaultIfEmpty(
                    0)
                .Max();

        return new DocumentExtractionPage(
            physicalPageNumber,
            ContentOrderTextExtractor.GetText(
                page),
            resolvedCoordinateSpace.ContentViewport,
            wordCount:
                documentWords.Length,
            rasterImageCount:
                images.Length,
            largestRasterImageAreaRatio:
                largestRasterImageAreaRatio,
            sourceWidth:
                sourceWidth,
            sourceHeight:
                sourceHeight,
            words:
                documentWords,
            blocks:
                documentBlocks);
    }

    private static DocumentWord ToDocumentWord(
        Word word,
        int sourceSequence,
        PdfPageCoordinateSpace coordinateSpace) =>
        new(
            sourceSequence,
            word.Text,
            coordinateSpace.ToNormalizedRectangle(
                word.BoundingBox),
            word.FontName,
            GetMedianPointSize(
                word.Letters));

    private static DocumentTextBlock ToDocumentTextBlock(
        TextBlock block,
        int sourceSequence,
        IReadOnlyDictionary<Word, DocumentWord> documentWordBySourceWord,
        PdfPageCoordinateSpace coordinateSpace)
    {
        var sourceWords =
            block.TextLines
                .SelectMany(line =>
                    line.Words)
                .ToArray();

        var words =
            sourceWords
                .Select(word =>
                    documentWordBySourceWord.TryGetValue(
                        word,
                        out var documentWord)
                        ? documentWord
                        : throw new InvalidDataException(
                            "PdfPig layout analysis returned a word that was not present " +
                            "in the native word extraction result."))
                .ToArray();

        var letters =
            sourceWords
                .SelectMany(word =>
                    word.Letters)
                .ToArray();

        return new DocumentTextBlock(
            sourceSequence,
            block.ReadingOrder >= 0
                ? block.ReadingOrder
                : null,
            block.Text,
            coordinateSpace.ToNormalizedRectangle(
                block.BoundingBox),
            words,
            GetDominantFontName(
                letters),
            GetMedianPointSize(
                letters),
            block.TextLines.Count);
    }

    private static string? GetDominantFontName(
        IReadOnlyCollection<Letter> letters) =>
        letters
            .Select(letter =>
                letter.FontName)
            .OfType<string>()
            .Where(fontName =>
                !string.IsNullOrWhiteSpace(
                    fontName))
            .GroupBy(
                fontName =>
                    fontName,
                StringComparer.Ordinal)
            .OrderByDescending(group =>
                group.Count())
            .ThenBy(
                group =>
                    group.Key,
                StringComparer.Ordinal)
            .Select(group =>
                group.Key)
            .FirstOrDefault();

    private static double? GetMedianPointSize(
        IReadOnlyCollection<Letter> letters)
    {
        var pointSizes =
            letters
                .Select(letter =>
                    letter.PointSize)
                .Where(pointSize =>
                    pointSize > 0 &&
                    double.IsFinite(
                        pointSize))
                .OrderBy(pointSize =>
                    pointSize)
                .ToArray();

        if (pointSizes.Length == 0)
        {
            return null;
        }

        var middle =
            pointSizes.Length / 2;

        return pointSizes.Length % 2 == 0
            ? (pointSizes[middle - 1] +
               pointSizes[middle]) / 2.0
            : pointSizes[middle];
    }


}
