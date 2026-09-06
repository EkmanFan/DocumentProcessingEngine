using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentProcessing.Core.Locations;
using DocumentProcessing.Core.Results;
using DocumentProcessing.Epub.Locations;

namespace DocumentProcessing.Manager.DPEngine;

/// <summary>
/// JSON encoding strategy for canonical portable document-processing results.
/// </summary>
/// <remarks>
/// Encoding is write-only and covers the source-structure families the engine
/// actually produces today: paged sources such as PDF, and EPUB package and
/// spine structure. A document is never coerced into a family it does not
/// belong to, so an EPUB is not given invented physical pages in order to be
/// transportable.
///
/// Each structure and location is written under an explicit <c>kind</c>
/// discriminator. A family this encoder does not know fails closed with a
/// serialization error rather than being dropped, coerced, or written as an
/// empty placeholder: silently losing structure at the transport boundary would
/// be indistinguishable, downstream, from a document that never had any.
/// </remarks>
public sealed class DocumentProcessingResultJsonEncoder
    : IDocumentProcessingResultEncoder
{
    #region Variables and Constants

    private const string PagedKind =
        "paged";

    private const string EpubKind =
        "epub";

    private const string EpubVisualKind =
        "epub-visual";

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

        // No structure-family guard is applied here. The converters below are
        // the single place that decides what can be encoded, so an unsupported
        // family fails in exactly one place instead of two that could drift.
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
            new DocumentSourceStructureJsonConverter());

        options.Converters.Add(
            new DocumentSourceLocationJsonConverter());

        return options;
    }

    /// <summary>
    /// Writes a property only when the value carries text, matching the
    /// encoder's null-omitting policy for the rest of the payload.
    /// </summary>
    private static void WriteOptionalString(
        Utf8JsonWriter writer,
        string propertyName,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(
                value))
        {
            writer.WriteString(
                propertyName,
                value);
        }
    }

    #endregion

    #region Types

    private sealed class DocumentSourceStructureJsonConverter
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
            switch (value)
            {
                case PagedDocumentSourceStructure paged:
                    WritePaged(
                        writer,
                        paged,
                        options);
                    return;
                case EpubDocumentSourceStructure epub:
                    WriteEpub(
                        writer,
                        epub,
                        options);
                    return;
                default:
                    throw new NotSupportedException(
                        "Portable result encoding does not support source " +
                        $"structure '{value.GetType().Name}'.");
            }
        }

        private static void WritePaged(
            Utf8JsonWriter writer,
            PagedDocumentSourceStructure paged,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(
                "kind",
                PagedKind);
            writer.WriteNumber(
                "sourcePhysicalPageCount",
                paged.SourcePhysicalPageCount);
            writer.WritePropertyName(
                "pages");
            JsonSerializer.Serialize(
                writer,
                paged.Pages,
                options);
            writer.WriteEndObject();
        }

        private static void WriteEpub(
            Utf8JsonWriter writer,
            EpubDocumentSourceStructure epub,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(
                "kind",
                EpubKind);
            writer.WriteString(
                "packagePath",
                epub.PackagePath);

            WriteOptionalString(
                writer,
                "title",
                epub.Title);

            WriteOptionalString(
                writer,
                "identifier",
                epub.Identifier);

            WriteOptionalString(
                writer,
                "language",
                epub.Language);

            if (epub.BodyMatterStartSpineIndex is not null)
            {
                writer.WriteNumber(
                    "bodyMatterStartSpineIndex",
                    epub.BodyMatterStartSpineIndex.Value);
            }

            writer.WritePropertyName(
                "spineItems");
            JsonSerializer.Serialize(
                writer,
                epub.SpineItems,
                options);

            writer.WriteEndObject();
        }
    }

    private sealed class DocumentSourceLocationJsonConverter
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
            switch (value)
            {
                case PagedDocumentSourceLocation paged:
                    WritePaged(
                        writer,
                        paged,
                        options);
                    return;
                case EpubDocumentSourceLocation epub:
                    WriteEpub(
                        writer,
                        epub);
                    return;
                case EpubVisualSourceLocation visual:
                    WriteEpubVisual(
                        writer,
                        visual);
                    return;
                default:
                    throw new NotSupportedException(
                        "Portable result encoding does not support source " +
                        $"location '{value.GetType().Name}'.");
            }
        }

        private static void WritePaged(
            Utf8JsonWriter writer,
            PagedDocumentSourceLocation paged,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString(
                "kind",
                PagedKind);
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

        private static void WriteEpub(
            Utf8JsonWriter writer,
            EpubDocumentSourceLocation epub)
        {
            writer.WriteStartObject();
            writer.WriteString(
                "kind",
                EpubKind);
            writer.WriteNumber(
                "spineIndex",
                epub.SpineIndex);
            writer.WriteString(
                "resourcePath",
                epub.ResourcePath);
            writer.WriteNumber(
                "blockIndex",
                epub.BlockIndex);

            WriteOptionalString(
                writer,
                "fragmentId",
                epub.FragmentId);

            writer.WriteEndObject();
        }

        /// <summary>
        /// A visual carries its own coordinates: the content resource that
        /// references it, the image resource itself, and which occurrence it is.
        /// They are distinct from a text position and are written under their
        /// own kind rather than flattened into one.
        /// </summary>
        private static void WriteEpubVisual(
            Utf8JsonWriter writer,
            EpubVisualSourceLocation visual)
        {
            writer.WriteStartObject();
            writer.WriteString(
                "kind",
                EpubVisualKind);
            writer.WriteNumber(
                "spineIndex",
                visual.SpineIndex);
            writer.WriteString(
                "contentResourcePath",
                visual.ContentResourcePath);
            writer.WriteString(
                "imageResourcePath",
                visual.ImageResourcePath);
            writer.WriteNumber(
                "occurrenceIndex",
                visual.OccurrenceIndex);
            writer.WriteBoolean(
                "isAuxiliary",
                visual.IsAuxiliary);

            WriteOptionalString(
                writer,
                "fragmentId",
                visual.FragmentId);

            writer.WriteEndObject();
        }
    }

    #endregion
}
