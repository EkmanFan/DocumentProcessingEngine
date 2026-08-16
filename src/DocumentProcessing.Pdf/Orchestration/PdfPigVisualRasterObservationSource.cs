using System.Globalization;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Orchestration;
using StbImageSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DocumentProcessing.Pdf;

/// <summary>
/// PdfPig-backed production source for deterministic low-level raster/geometry
/// observations of embedded PDF image occurrences.
///
/// The source measures evidence only. It does not classify semantic visual kind,
/// decide disposition, invoke layout ML or invoke OCR.
/// </summary>
public sealed class PdfPigVisualRasterObservationSource
    : IVisualRasterObservationSource
{
    /// <summary>
    /// Operational safety ceiling for one decoded RGBA source image.
    ///
    /// Sixteen million pixels correspond to 64 MiB for the final RGBA byte
    /// buffer alone. Images above the ceiling fail closed as unavailable visual
    /// evidence rather than being decoded without bound.
    /// </summary>
    public const long DefaultMaxDecodedPixels =
        16_000_000;

    private readonly long _maxDecodedPixels;
    private readonly PdfVisualRasterMeasurementEngine _measurementEngine =
        new();

    public PdfPigVisualRasterObservationSource(
        long maxDecodedPixels = DefaultMaxDecodedPixels)
    {
        if (maxDecodedPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDecodedPixels),
                maxDecodedPixels,
                "Decoded-pixel ceiling must be positive.");
        }

        _maxDecodedPixels =
            maxDecodedPixels;
    }

    public bool CanObserve(
        DocumentFormatId format) =>
        format ==
        DocumentFormatId.Pdf;

    public async ValueTask<IReadOnlyList<PageVisualRasterObservations>>
        ObserveAsync(
            DocumentSource source,
            DocumentFormatId format,
            DocumentExtractionResult extraction,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            extraction);

        if (!CanObserve(
                format))
        {
            throw new NotSupportedException(
                $"Format '{format}' is not supported by the PDF visual raster observer.");
        }

        if (extraction.Format !=
            format)
        {
            throw new InvalidDataException(
                $"Extraction format '{extraction.Format}' does not match requested " +
                $"visual-observation format '{format}'.");
        }

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

            if (document.NumberOfPages !=
                extraction.Pages.Count)
            {
                throw new InvalidDataException(
                    $"PDF contains {document.NumberOfPages} page(s), but extraction " +
                    $"contains {extraction.Pages.Count} page(s).");
            }

            var pages =
                new List<PageVisualRasterObservations>(
                    document.NumberOfPages);

            var physicalPageNumber =
                0;

            foreach (var page in
                     document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                physicalPageNumber++;

                var extractionPage =
                    extraction.Pages[
                        physicalPageNumber -
                        1];

                var coordinateSpace =
                    PdfPageCoordinateSpace.Create(
                        page);

                var images =
                    page.GetImages()
                        .ToArray();

                pages.Add(
                    ObservePage(
                        physicalPageNumber,
                        coordinateSpace,
                        images,
                        extractionPage,
                        cancellationToken));
            }

            return pages;
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

    internal PageVisualRasterObservations ObservePage(
        int physicalPageNumber,
        PdfPageCoordinateSpace coordinateSpace,
        IReadOnlyList<IPdfImage> images,
        DocumentExtractionPage extractionPage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            images);

        ArgumentNullException.ThrowIfNull(
            extractionPage);

        cancellationToken.ThrowIfCancellationRequested();

        if (extractionPage.PhysicalPageNumber !=
            physicalPageNumber)
        {
            throw new InvalidDataException(
                $"Extraction page reports physical page " +
                $"{extractionPage.PhysicalPageNumber}; expected " +
                $"{physicalPageNumber}.");
        }

        if (images.Count !=
            extractionPage.RasterImageCount)
        {
            throw new InvalidDataException(
                $"Physical page {physicalPageNumber} exposes {images.Count} " +
                $"PDF image occurrence(s), but extraction recorded " +
                $"{extractionPage.RasterImageCount}.");
        }

        var observations =
            new VisualRasterObservation[
                images.Count];

        for (var imageIndex = 0;
             imageIndex <
             images.Count;
             imageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            observations[imageIndex] =
                ObserveImage(
                    imageIndex,
                    images[imageIndex],
                    coordinateSpace,
                    extractionPage.Words,
                    cancellationToken);
        }

        return new PageVisualRasterObservations(
            physicalPageNumber,
            observations);
    }

    private VisualRasterObservation ObserveImage(
        int sourceVisualIndex,
        IPdfImage image,
        PdfPageCoordinateSpace coordinateSpace,
        IReadOnlyList<DocumentWord> nativeWords,
        CancellationToken cancellationToken)
    {
        var declaredBounds =
            coordinateSpace.ToNormalizedRectangle(
                image.BoundingBox);

        var sampleWidth =
            Convert.ToInt64(
                image.WidthInSamples,
                CultureInfo.InvariantCulture);

        var sampleHeight =
            Convert.ToInt64(
                image.HeightInSamples,
                CultureInfo.InvariantCulture);

        if (sampleWidth >
                0 &&
            sampleHeight >
                0 &&
            ExceedsPixelBudget(
                sampleWidth,
                sampleHeight))
        {
            return Unavailable(
                sourceVisualIndex,
                declaredBounds);
        }

        if (!TryDecode(
                image,
                cancellationToken,
                out var raster,
                out var decodeSource))
        {
            return Unavailable(
                sourceVisualIndex,
                declaredBounds);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (ExceedsPixelBudget(
                raster!.Width,
                raster.Height))
        {
            return Unavailable(
                sourceVisualIndex,
                declaredBounds);
        }

        return _measurementEngine.Measure(
            sourceVisualIndex,
            declaredBounds,
            decodeSource,
            raster.Width,
            raster.Height,
            raster.Data,
            nativeWords,
            cancellationToken);
    }

    private bool ExceedsPixelBudget(
        long width,
        long height)
    {
        if (width <=
                0 ||
            height <=
                0)
        {
            return false;
        }

        return width >
                   _maxDecodedPixels /
                   height;
    }

    private static bool TryDecode(
        IPdfImage image,
        CancellationToken cancellationToken,
        out ImageResult? raster,
        out VisualRasterDecodeSource decodeSource)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rawBytes =
            image.RawBytes.ToArray();

        if (TryDecodeBytes(
                rawBytes,
                out raster))
        {
            decodeSource =
                VisualRasterDecodeSource.RawEmbeddedImage;

            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (image.TryGetPng(
                    out var pngBytes) &&
                TryDecodeBytes(
                    pngBytes,
                    out raster))
            {
                decodeSource =
                    VisualRasterDecodeSource.PdfPigPng;

                return true;
            }
        }
        catch (Exception exception)
            when (exception is not
                  OutOfMemoryException)
        {
            // Unsupported/corrupt image decoding is evidence unavailability,
            // not permission to guess a visual subtype.
        }

        raster =
            null;

        decodeSource =
            VisualRasterDecodeSource.Unavailable;

        return false;
    }

    private static bool TryDecodeBytes(
        byte[] bytes,
        out ImageResult? raster)
    {
        try
        {
            raster =
                ImageResult.FromMemory(
                    bytes,
                    ColorComponents.RedGreenBlueAlpha);

            return true;
        }
        catch (Exception exception)
            when (exception is not
                  OutOfMemoryException)
        {
            raster =
                null;

            return false;
        }
    }

    private static VisualRasterObservation Unavailable(
        int sourceVisualIndex,
        NormalizedRectangle declaredBounds) =>
        new(
            sourceVisualIndex,
            declaredBounds,
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
}
