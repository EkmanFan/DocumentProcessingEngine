using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Core.Results.Serialization;

/// <summary>
/// Official UTF-8 JSON V1 boundary for <see cref="DocumentIngestionResult"/>.
///
/// The domain model is not serialized directly. An internal explicit transport
/// contract fixes JSON names, omits derived duplicate properties and maps all
/// untrusted input back through the validated public result constructors.
/// </summary>
public static class DocumentIngestionResultJson
{
    #region Variables and Constants

    private static readonly JsonSerializerOptions Options =
        CreateOptions();

    #endregion


    #region Methods

    public static byte[] SerializeToUtf8Bytes(
        DocumentIngestionResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        var contract =
            DocumentIngestionResultJsonContract
                .FromModel(
                    result);

        return JsonSerializer.SerializeToUtf8Bytes(
            contract,
            Options);
    }

    public static DocumentIngestionResult Deserialize(
        ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
        {
            throw new JsonException(
                "Document ingestion result JSON cannot be empty.");
        }

        var contract =
            JsonSerializer.Deserialize<
                DocumentIngestionResultJsonContract>(
                utf8Json,
                Options) ??
            throw new JsonException(
                "Document ingestion result JSON must contain an object.");

        if (!string.Equals(
                contract.SchemaVersion,
                DocumentIngestionResult.SchemaVersionId,
                StringComparison.Ordinal))
        {
            throw new UnsupportedDocumentIngestionResultSchemaException(
                contract.SchemaVersion);
        }

        try
        {
            return contract.ToModel();
        }
        catch (UnsupportedDocumentIngestionResultSchemaException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  InvalidOperationException or
                  KeyNotFoundException)
        {
            throw new JsonException(
                "Document ingestion result JSON violates the portable result invariants.",
                exception);
        }
    }

    private static JsonSerializerOptions CreateOptions() =>
        new()
        {
            AllowDuplicateProperties = false,
            AllowTrailingCommas = false,
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 64,
            NumberHandling =
                JsonNumberHandling.Strict,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling =
                JsonCommentHandling.Disallow,
            RespectNullableAnnotations = true,
            UnmappedMemberHandling =
                JsonUnmappedMemberHandling.Skip,
            WriteIndented = false
        };

    #endregion
}
