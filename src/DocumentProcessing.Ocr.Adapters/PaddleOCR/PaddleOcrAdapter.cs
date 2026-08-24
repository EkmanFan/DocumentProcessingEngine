using System.Text.Json;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Ocr;
using DocumentProcessing.Core.Raster;

namespace DocumentProcessing.Ocr.Adapters.PaddleOCR;

/// <summary>
/// Adapts the neutral targeted-OCR contract to PaddleOCR and translates
/// provider-native recognition evidence back to the Core OCR model.
///
/// The Engine owns the policy that decides whether OCR is appropriate. This
/// adapter performs contract validation and translation only.
/// </summary>
public sealed class PaddleOcrAdapter
    : IRegionTextRecognizer
{
    #region Variables and Constants

    public const string BackendId =
        "paddleocr-general-ocr";

    private readonly PaddleOcrServingClient _client;
    private readonly string _profileId;

    #endregion


    #region ctor

    public PaddleOcrAdapter(
        PaddleOcrServingClient client,
        string profileId)
    {
        _client =
            client ??
            throw new ArgumentNullException(
                nameof(client));

        if (string.IsNullOrWhiteSpace(
                profileId))
        {
            throw new ArgumentException(
                "OCR profile ID cannot be empty.",
                nameof(profileId));
        }

        _profileId =
            profileId.Trim();
    }

    #endregion


    #region Methods

    public async ValueTask<OcrRegionResult> RecognizeAsync(
        Stream rasterRegion,
        LayoutObservation sourceLayoutObservation,
        PixelRectangle crop,
        int pagePixelWidth,
        int pagePixelHeight,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            rasterRegion);

        ArgumentNullException.ThrowIfNull(
            sourceLayoutObservation);

        if (pagePixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pagePixelWidth));
        }

        if (pagePixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pagePixelHeight));
        }

        var expectedCrop =
            RasterCropGeometry.FromNormalized(
                sourceLayoutObservation.Bounds,
                pagePixelWidth,
                pagePixelHeight);

        if (crop != expectedCrop)
        {
            throw new ArgumentException(
                "Raster crop does not match the deterministic crop derived " +
                "from the source layout observation.",
                nameof(crop));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var nativeResult =
            await _client
                .RecognizeAsync(
                    rasterRegion,
                    cancellationToken)
                .ConfigureAwait(false);

        return Adapt(
            nativeResult,
            sourceLayoutObservation,
            crop,
            pagePixelWidth,
            pagePixelHeight);
    }

    private OcrRegionResult Adapt(
        PaddleOcrNativeResult nativeResult,
        LayoutObservation sourceLayoutObservation,
        PixelRectangle crop,
        int pagePixelWidth,
        int pagePixelHeight)
    {
        ArgumentNullException.ThrowIfNull(
            nativeResult);

        try
        {
            using var document =
                JsonDocument.Parse(
                    nativeResult.PrunedResultJson);

            var prunedResult =
                document.RootElement;

            if (prunedResult.ValueKind !=
                JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "PaddleOCR prunedResult must be an object.");
            }

            var texts =
                ReadRequiredArray(
                    prunedResult,
                    "rec_texts");

            var scores =
                ReadRequiredArray(
                    prunedResult,
                    "rec_scores");

            var boxes =
                ReadRequiredArray(
                    prunedResult,
                    "rec_boxes");

            if (texts.GetArrayLength() !=
                    scores.GetArrayLength() ||
                texts.GetArrayLength() !=
                    boxes.GetArrayLength())
            {
                throw new InvalidDataException(
                    "PaddleOCR rec_texts, rec_scores and rec_boxes must have " +
                    "the same length.");
            }

            var observations =
                new List<OcrTextObservation>();

            for (var index = 0;
                 index < texts.GetArrayLength();
                 index++)
            {
                var textElement =
                    texts[index];

                if (textElement.ValueKind !=
                    JsonValueKind.String)
                {
                    throw new InvalidDataException(
                        $"PaddleOCR rec_texts[{index}] must be a string.");
                }

                var scoreElement =
                    scores[index];

                if (scoreElement.ValueKind !=
                        JsonValueKind.Number ||
                    !scoreElement.TryGetDouble(
                        out var confidence))
                {
                    throw new InvalidDataException(
                        $"PaddleOCR rec_scores[{index}] must be numeric.");
                }

                var localBox =
                    ReadBox(
                        boxes[index],
                        index);

                var text =
                    textElement.GetString();

                if (string.IsNullOrWhiteSpace(
                        text))
                {
                    continue;
                }

                observations.Add(
                    new OcrTextObservation(
                        sourceLayoutObservation.PhysicalPageNumber,
                        sourceLayoutObservation.ObservationSequence,
                        index,
                        text,
                        confidence,
                        MapToPageBounds(
                            localBox,
                            crop,
                            pagePixelWidth,
                            pagePixelHeight)));
            }

            return new OcrRegionResult(
                BackendId,
                _profileId,
                sourceLayoutObservation,
                observations);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "PaddleOCR native result is not valid JSON.",
                exception);
        }
    }

    private static NormalizedRectangle MapToPageBounds(
        LocalBox localBox,
        PixelRectangle crop,
        int pagePixelWidth,
        int pagePixelHeight)
    {
        var left =
            (crop.Left + localBox.Left) /
            (double)pagePixelWidth;

        var top =
            (crop.Top + localBox.Top) /
            (double)pagePixelHeight;

        var right =
            (crop.Left + localBox.Right) /
            (double)pagePixelWidth;

        var bottom =
            (crop.Top + localBox.Bottom) /
            (double)pagePixelHeight;

        return new NormalizedRectangle(
            left,
            top,
            right,
            bottom);
    }

    private static LocalBox ReadBox(
        JsonElement element,
        int index)
    {
        if (element.ValueKind !=
                JsonValueKind.Array ||
            element.GetArrayLength() != 4)
        {
            throw new InvalidDataException(
                $"PaddleOCR rec_boxes[{index}] must contain four numbers.");
        }

        var values =
            new double[4];

        for (var coordinate = 0;
             coordinate < values.Length;
             coordinate++)
        {
            var value =
                element[coordinate];

            if (value.ValueKind !=
                    JsonValueKind.Number ||
                !value.TryGetDouble(
                    out values[coordinate]) ||
                !double.IsFinite(
                    values[coordinate]))
            {
                throw new InvalidDataException(
                    $"PaddleOCR rec_boxes[{index}][{coordinate}] must be " +
                    "a finite number.");
            }
        }

        if (values[2] < values[0] ||
            values[3] < values[1])
        {
            throw new InvalidDataException(
                $"PaddleOCR rec_boxes[{index}] has reversed coordinates.");
        }

        return new LocalBox(
            values[0],
            values[1],
            values[2],
            values[3]);
    }

    private static JsonElement ReadRequiredArray(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind !=
                JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"PaddleOCR prunedResult has no valid {propertyName} array.");
        }

        return property;
    }

    private readonly record struct LocalBox(
        double Left,
        double Top,
        double Right,
        double Bottom);

    #endregion
}
