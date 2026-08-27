using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Manager.DPEngine;

/// <summary>
/// JSON encoding strategy for canonical results backed by physical pages.
/// </summary>
public sealed class PagedDocumentProcessingResultJsonEncoder
    : IDocumentProcessingResultEncoder
{
    #region Variables and Constants

    private static readonly JsonSerializerOptions
        SerializerOptions =
            CreateSerializerOptions();

    #endregion

    #region Properties

    /// <inheritdoc />
    public string MediaType =>
        "application/vnd.document-processing-result+json";

    /// <inheritdoc />
    public string SchemaVersion =>
        DocumentProcessingResult.SchemaVersionId;

    #endregion

    #region Methods

    /// <inheritdoc />
    public byte[] Encode(
        DocumentProcessingResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        if (result.SourceStructure is not PagedDocumentSourceStructure)
        {
            throw new NotSupportedException(
                "Managed execution V1 can encode only paged document-processing results.");
        }

        return JsonSerializer.SerializeToUtf8Bytes(
            result,
            SerializerOptions);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition =
                    JsonIgnoreCondition.WhenWritingNull
            };

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase));

        options.Converters.Add(
            new PagedDocumentSourceStructureJsonConverter());

        options.Converters.Add(
            new PagedDocumentSourceLocationJsonConverter());

        return options;
    }

    #endregion

    #region Types

    private sealed class PagedDocumentSourceStructureJsonConverter
        : JsonConverter<DocumentSourceStructure>
    {
        public override DocumentSourceStructure Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            throw new NotSupportedException(
                "Managed result encoding is write-only.");

        public override void Write(
            Utf8JsonWriter writer,
            DocumentSourceStructure value,
            JsonSerializerOptions options)
        {
            if (value is not PagedDocumentSourceStructure paged)
            {
                throw new NotSupportedException(
                    "Managed execution V1 can encode only paged source structures.");
            }

            writer.WriteStartObject();
            writer.WriteString(
                "kind",
                "paged");
            writer.WritePropertyName(
                "pages");
            JsonSerializer.Serialize(
                writer,
                paged.Pages,
                options);
            writer.WriteEndObject();
        }
    }

    private sealed class PagedDocumentSourceLocationJsonConverter
        : JsonConverter<DocumentSourceLocation>
    {
        public override DocumentSourceLocation Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            throw new NotSupportedException(
                "Managed result encoding is write-only.");

        public override void Write(
            Utf8JsonWriter writer,
            DocumentSourceLocation value,
            JsonSerializerOptions options)
        {
            if (value is not PagedDocumentSourceLocation paged)
            {
                throw new NotSupportedException(
                    "Managed execution V1 can encode only paged source locations.");
            }

            writer.WriteStartObject();
            writer.WriteString(
                "kind",
                "paged");
            writer.WriteNumber(
                "physicalPageNumber",
                paged.PhysicalPageNumber);

            if (paged.Bounds is not null)
            {
                writer.WritePropertyName(
                    "bounds");
                JsonSerializer.Serialize(
                    writer,
                    paged.Bounds.Value,
                    options);
            }

            writer.WriteEndObject();
        }
    }

    #endregion
}
