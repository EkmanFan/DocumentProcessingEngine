using System.Globalization;
using System.Security.Cryptography;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Visual;
using StbImageSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace DocumentProcessing.Pdf;

/// <summary>
/// PdfPig-backed materializer for one exact embedded PDF image occurrence.
///
/// Source visual identity follows the same deterministic per-page image ordering
/// used by <see cref="PdfPigVisualRasterObservationSource"/>:
///
/// <code>
/// page.GetImages().ToArray()
/// </code>
///
/// For a JPEG source image, PdfPig exposes the directly embedded JPEG through
/// <see cref="IPdfImage.RawBytes"/>. This implementation preserves those exact
/// bytes only after validating JPEG signature, standalone decode, and exact
/// source sample dimensions.
///
/// Other PDF image streams remain opaque and are never mislabeled as standalone
/// image files. They use PdfPig's PNG conversion fallback.
///
/// The materializer does not invoke layout analysis, OCR, semantic visual
/// classification, or document routing.
/// </summary>
public sealed class PdfPigSourceVisualAssetMaterializer
    : ISourceVisualAssetMaterializer
{
    public const long DefaultMaxSourcePixels =
        PdfPigVisualRasterObservationSource.DefaultMaxDecodedPixels;

    public const long DefaultMaxOutputBytes =
        64L *
        1024L *
        1024L;

    public const string RawJpegProfileId =
        "pdfpig-0.1.15-source-visual-raw-jpeg-v1";

    public const string PngProfileId =
        "pdfpig-0.1.15-source-visual-png-fallback-v1";

    private const string JpegMediaType =
        "image/jpeg";

    private const string PngMediaType =
        "image/png";

    private static readonly byte[] JpegSignature =
    [
        0xFF,
        0xD8,
        0xFF
    ];

    private static readonly byte[] PngSignature =
    [
        0x89,
        0x50,
        0x4E,
        0x47,
        0x0D,
        0x0A,
        0x1A,
        0x0A
    ];

    private readonly long _maxSourcePixels;
    private readonly long _maxOutputBytes;

    public PdfPigSourceVisualAssetMaterializer(
        long maxSourcePixels = DefaultMaxSourcePixels,
        long maxOutputBytes = DefaultMaxOutputBytes)
    {
        if (maxSourcePixels <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSourcePixels),
                maxSourcePixels,
                "Source-pixel ceiling must be positive.");
        }

        if (maxOutputBytes <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxOutputBytes),
                maxOutputBytes,
                "Output-byte ceiling must be positive.");
        }

        _maxSourcePixels =
            maxSourcePixels;

        _maxOutputBytes =
            maxOutputBytes;
    }

    public bool CanMaterialize(
        DocumentFormatId format) =>
        format ==
        DocumentFormatId.Pdf;

    public async ValueTask<SourceVisualAssetMaterialization> MaterializeAsync(
        DocumentSource source,
        DocumentFormatId format,
        DocumentExtractionResult extraction,
        int physicalPageNumber,
        int sourceVisualIndex,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            extraction);

        ArgumentNullException.ThrowIfNull(
            destination);

        if (!CanMaterialize(
                format))
        {
            throw new NotSupportedException(
                $"Format '{format}' is not supported by the PDF source visual materializer.");
        }

        if (extraction.Format !=
            format)
        {
            throw new InvalidDataException(
                $"Extraction format '{extraction.Format}' does not match requested " +
                $"source-visual materialization format '{format}'.");
        }

        if (physicalPageNumber <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber),
                physicalPageNumber,
                "Physical page number must be positive.");
        }

        if (sourceVisualIndex <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVisualIndex),
                sourceVisualIndex,
                "Source visual index must be non-negative.");
        }

        if (ReferenceEquals(
                source.Content,
                destination))
        {
            throw new ArgumentException(
                "Document source and visual destination streams must be different.",
                nameof(destination));
        }

        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "Source visual destination stream must be writable.",
                nameof(destination));
        }

        if (destination.CanSeek &&
            (
                destination.Position !=
                0 ||
                destination.Length !=
                0
            ))
        {
            throw new ArgumentException(
                "Seekable source visual destinations must be empty and positioned at zero.",
                nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var input =
            source.Content;

        MemoryStream? bufferedInput =
            null;

        long? originalSourcePosition =
            null;

        try
        {
            if (input.CanSeek)
            {
                originalSourcePosition =
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

            if (physicalPageNumber >
                document.NumberOfPages)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(physicalPageNumber),
                    physicalPageNumber,
                    $"PDF contains only {document.NumberOfPages} page(s).");
            }

            var extractionPage =
                extraction.Pages[
                    physicalPageNumber -
                    1];

            if (extractionPage.PhysicalPageNumber !=
                physicalPageNumber)
            {
                throw new InvalidDataException(
                    $"Extraction page reports physical page " +
                    $"{extractionPage.PhysicalPageNumber}; expected " +
                    $"{physicalPageNumber}.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var page =
                document.GetPage(
                    physicalPageNumber);

            var images =
                page.GetImages()
                    .ToArray();

            if (images.Length !=
                extractionPage.RasterImageCount)
            {
                throw new InvalidDataException(
                    $"Physical page {physicalPageNumber} exposes {images.Length} " +
                    $"PDF image occurrence(s), but extraction recorded " +
                    $"{extractionPage.RasterImageCount}.");
            }

            if (sourceVisualIndex >=
                images.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceVisualIndex),
                    sourceVisualIndex,
                    $"Physical page {physicalPageNumber} exposes {images.Length} " +
                    "source visual occurrence(s).");
            }

            var image =
                images[
                    sourceVisualIndex];

            ValidateSourcePixelBudget(
                image);

            var declaredBounds =
                PdfPageCoordinateSpace
                    .Create(
                        page)
                    .ToNormalizedRectangle(
                        image.BoundingBox);

            cancellationToken.ThrowIfCancellationRequested();

            var standalone =
                MaterializeStandaloneImage(
                    image,
                    physicalPageNumber,
                    sourceVisualIndex,
                    cancellationToken);

            if (standalone.Bytes.LongLength >
                _maxOutputBytes)
            {
                throw new InvalidDataException(
                    $"Materialized source visual exceeds the {_maxOutputBytes}-byte output limit.");
            }

            var contentSha256 =
                Convert
                    .ToHexString(
                        SHA256.HashData(
                            standalone.Bytes))
                    .ToLowerInvariant();

            cancellationToken.ThrowIfCancellationRequested();

            await destination
                .WriteAsync(
                    standalone.Bytes.AsMemory(),
                    cancellationToken)
                .ConfigureAwait(false);

            await destination
                .FlushAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            return new SourceVisualAssetMaterialization(
                physicalPageNumber,
                sourceVisualIndex,
                declaredBounds,
                standalone.ProfileId,
                standalone.MediaType,
                standalone.Bytes.LongLength,
                contentSha256);
        }
        catch
        {
            ResetDestination(
                destination);

            throw;
        }
        finally
        {
            bufferedInput?.Dispose();

            if (originalSourcePosition.HasValue)
            {
                source.Content.Position =
                    originalSourcePosition.Value;
            }
        }
    }

    private StandaloneImage MaterializeStandaloneImage(
        IPdfImage image,
        int physicalPageNumber,
        int sourceVisualIndex,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (TryGetValidatedRawJpeg(
                image,
                out var jpegBytes))
        {
            return new StandaloneImage(
                jpegBytes!,
                RawJpegProfileId,
                JpegMediaType);
        }

        cancellationToken.ThrowIfCancellationRequested();

        byte[]? pngBytes;

        try
        {
            if (!image.TryGetPng(
                    out pngBytes) ||
                pngBytes is null ||
                pngBytes.Length ==
                    0)
            {
                throw new InvalidDataException(
                    $"Physical page {physicalPageNumber}, source visual " +
                    $"{sourceVisualIndex} is neither a validated embedded JPEG " +
                    "nor convertible by PdfPig to PNG.");
            }
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidDataException(
                $"Physical page {physicalPageNumber}, source visual " +
                $"{sourceVisualIndex} cannot be materialized as a standalone image.",
                exception);
        }

        if (pngBytes.LongLength >
            _maxOutputBytes)
        {
            throw new InvalidDataException(
                $"Materialized source visual exceeds the {_maxOutputBytes}-byte output limit.");
        }

        if (!HasSignature(
                pngBytes,
                PngSignature))
        {
            throw new InvalidDataException(
                "PdfPig source visual fallback did not produce a valid PNG signature.");
        }

        ValidateDecodedDimensions(
            image,
            pngBytes,
            "PdfPig PNG fallback");

        return new StandaloneImage(
            pngBytes,
            PngProfileId,
            PngMediaType);
    }

    private bool TryGetValidatedRawJpeg(
        IPdfImage image,
        out byte[]? jpegBytes)
    {
        jpegBytes =
            null;

        byte[] rawBytes;

        try
        {
            rawBytes =
                image.RawBytes.ToArray();
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch
        {
            return false;
        }

        if (rawBytes.Length ==
                0 ||
            rawBytes.LongLength >
                _maxOutputBytes ||
            !HasSignature(
                rawBytes,
                JpegSignature))
        {
            return false;
        }

        try
        {
            ValidateDecodedDimensions(
                image,
                rawBytes,
                "Embedded JPEG");
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch
        {
            return false;
        }

        jpegBytes =
            rawBytes;

        return true;
    }

    private static void ValidateDecodedDimensions(
        IPdfImage image,
        byte[] bytes,
        string sourceDescription)
    {
        ImageResult decoded;

        try
        {
            decoded =
                ImageResult.FromMemory(
                    bytes,
                    ColorComponents.RedGreenBlueAlpha);
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidDataException(
                $"{sourceDescription} cannot be decoded as a standalone image.",
                exception);
        }

        var expectedWidth =
            Convert.ToInt64(
                image.WidthInSamples,
                CultureInfo.InvariantCulture);

        var expectedHeight =
            Convert.ToInt64(
                image.HeightInSamples,
                CultureInfo.InvariantCulture);

        if (decoded.Width !=
                expectedWidth ||
            decoded.Height !=
                expectedHeight)
        {
            throw new InvalidDataException(
                $"{sourceDescription} decoded to {decoded.Width}x{decoded.Height}, " +
                $"but the PDF source visual declares {expectedWidth}x{expectedHeight} samples.");
        }
    }

    private void ValidateSourcePixelBudget(
        IPdfImage image)
    {
        var width =
            Convert.ToInt64(
                image.WidthInSamples,
                CultureInfo.InvariantCulture);

        var height =
            Convert.ToInt64(
                image.HeightInSamples,
                CultureInfo.InvariantCulture);

        if (width <=
                0 ||
            height <=
                0)
        {
            throw new InvalidDataException(
                "Source visual has invalid pixel dimensions.");
        }

        if (width >
            _maxSourcePixels /
            height)
        {
            throw new InvalidDataException(
                $"Source visual exceeds the {_maxSourcePixels}-pixel materialization limit.");
        }
    }

    private static bool HasSignature(
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<byte> signature) =>
        bytes.Length >=
            signature.Length &&
        bytes[
            ..signature.Length]
            .SequenceEqual(
                signature);

    private static void ResetDestination(
        Stream destination)
    {
        if (!destination.CanSeek)
        {
            return;
        }

        destination.SetLength(
            0);

        destination.Position =
            0;
    }

    private sealed record StandaloneImage(
        byte[] Bytes,
        string ProfileId,
        string MediaType);
}
