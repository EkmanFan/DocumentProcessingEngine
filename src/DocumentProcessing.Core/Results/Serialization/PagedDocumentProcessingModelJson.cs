using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentProcessing.Core.Results;

namespace DocumentProcessing.Core.Results.Serialization;

/// <summary>
/// Official UTF-8 JSON V1 boundary for <see cref="PagedDocumentProcessingModel"/>.
///
/// The domain model is not serialized directly. An internal explicit transport
/// contract fixes JSON names, omits derived duplicate properties and maps all
/// untrusted input back through the validated public result constructors.
/// </summary>
public static class PagedDocumentProcessingModelJson
{
    #region Variables and Constants

    private static readonly JsonSerializerOptions Options =
        CreateOptions();

    #endregion


    #region Methods

    public static byte[] SerializeToUtf8Bytes(
        PagedDocumentProcessingModel result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        var contract =
            PagedDocumentProcessingModelJsonContract
                .FromModel(
                    result);

        return JsonSerializer.SerializeToUtf8Bytes(
            contract,
            Options);
    }

    public static PagedDocumentProcessingModel Deserialize(
        ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
        {
            throw new JsonException(
                "Paged document processing model JSON cannot be empty.");
        }

        var contract =
            JsonSerializer.Deserialize<
                PagedDocumentProcessingModelJsonContract>(
                utf8Json,
                Options) ??
            throw new JsonException(
                "Paged document processing model JSON must contain an object.");

        if (!string.Equals(
                contract.SchemaVersion,
                PagedDocumentProcessingModel.SchemaVersionId,
                StringComparison.Ordinal))
        {
            throw new UnsupportedPagedDocumentProcessingModelSchemaException(
                contract.SchemaVersion);
        }

        try
        {
            return contract.ToModel();
        }
        catch (UnsupportedPagedDocumentProcessingModelSchemaException)
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
                "Paged document processing model JSON violates the model invariants.",
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
