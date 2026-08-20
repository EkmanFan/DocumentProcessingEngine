using System.Text.Json;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;

namespace DocumentProcessing.Engine.Layout;

/// <summary>
/// Adapts a single-page PP-StructureV3 JSON result to the neutral Core layout
/// model.
///
/// This class does not start Python, Paddle, Docker, or any inference runtime.
/// It is the narrow format boundary between PP-StructureV3 output and the
/// engine-owned neutral model.
/// </summary>
public sealed class PpStructureV3LayoutAdapter
{
    #region Variables and Constants

    public const string BackendId = "pp-structurev3";

    #endregion


    #region Methods

    public async ValueTask<LayoutAnalysisResult> AdaptAsync(
        Stream resultJson,
        int physicalPageNumber,
        int pixelWidth,
        int pixelHeight,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resultJson);

        if (physicalPageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (pixelWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        }

        if (pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight));
        }

        cancellationToken.ThrowIfCancellationRequested();

        long? originalPosition = null;

        if (resultJson.CanSeek)
        {
            originalPosition = resultJson.Position;
            resultJson.Position = 0;
        }

        try
        {
            using var document =
                await JsonDocument
                    .ParseAsync(
                        resultJson,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

            var result = ResolveResultElement(document.RootElement);

            if (!result.TryGetProperty(
                    "parsing_res_list",
                    out var parsingResults) ||
                parsingResults.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException(
                    "PP-StructureV3 result does not contain parsing_res_list.");
            }

            var observations =
                new List<LayoutObservation>(
                    parsingResults.GetArrayLength());

            var sequence = 0;

            foreach (var block in parsingResults.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (block.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException(
                        $"PP-StructureV3 parsing block {sequence} is not an object.");
                }

                var rawLabel = ReadRequiredString(
                    block,
                    "block_label",
                    sequence);

                var bounds = ReadBounds(
                    block,
                    sequence,
                    pixelWidth,
                    pixelHeight);

                observations.Add(
                    new LayoutObservation(
                        physicalPageNumber,
                        observationSequence: sequence,
                        readingOrder: sequence,
                        MapKind(rawLabel),
                        bounds,
                        rawLabel));

                sequence++;
            }

            return new LayoutAnalysisResult(
                BackendId,
                physicalPageNumber,
                observations);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "PP-StructureV3 result is not valid JSON.",
                exception);
        }
        finally
        {
            if (originalPosition.HasValue)
            {
                resultJson.Position = originalPosition.Value;
            }
        }
    }

    private static JsonElement ResolveResultElement(
        JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "PP-StructureV3 JSON root must be an object.");
        }

        if (root.TryGetProperty("res", out var nestedResult))
        {
            if (nestedResult.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "PP-StructureV3 res property must be an object.");
            }

            return nestedResult;
        }

        return root;
    }

    private static string ReadRequiredString(
        JsonElement block,
        string propertyName,
        int sequence)
    {
        if (!block.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"PP-StructureV3 parsing block {sequence} has no valid {propertyName}.");
        }

        var value = property.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"PP-StructureV3 parsing block {sequence} has an empty {propertyName}.");
        }

        return value;
    }

    private static NormalizedRectangle ReadBounds(
        JsonElement block,
        int sequence,
        int pixelWidth,
        int pixelHeight)
    {
        if (!block.TryGetProperty(
                "block_bbox",
                out var bbox) ||
            bbox.ValueKind != JsonValueKind.Array ||
            bbox.GetArrayLength() != 4)
        {
            throw new InvalidDataException(
                $"PP-StructureV3 parsing block {sequence} has no valid block_bbox.");
        }

        var values =
            bbox
                .EnumerateArray()
                .Select(
                    value =>
                        value.ValueKind == JsonValueKind.Number &&
                        value.TryGetDouble(out var number)
                            ? number
                            : throw new InvalidDataException(
                                $"PP-StructureV3 parsing block {sequence} contains " +
                                "a non-numeric block_bbox coordinate."))
                .ToArray();

        return new NormalizedRectangle(
            values[0] / pixelWidth,
            values[1] / pixelHeight,
            values[2] / pixelWidth,
            values[3] / pixelHeight);
    }

    private static LayoutObservationKind MapKind(
        string rawLabel) =>
        rawLabel.Trim().ToLowerInvariant() switch
        {
            "text" => LayoutObservationKind.Text,

            "paragraph_title" or
            "doc_title" =>
                LayoutObservationKind.Heading,

            "figure_title" or
            "figure_caption" =>
                LayoutObservationKind.Caption,

            "image" or
            "figure" or
            "header_image" or
            "footer_image" =>
                LayoutObservationKind.Figure,

            "table" =>
                LayoutObservationKind.Table,

            _ =>
                LayoutObservationKind.Unknown
        };

    #endregion
}
