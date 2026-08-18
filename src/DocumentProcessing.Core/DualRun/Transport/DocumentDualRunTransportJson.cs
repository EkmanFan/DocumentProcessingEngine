using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocumentProcessing.Core.DualRun.Transport;

/// <summary>
/// Strict UTF-8 JSON boundary for the local Dual Run V1 worker protocol.
///
/// Domain models are never serialized directly. Explicit transport DTOs fix all
/// wire names and map untrusted JSON back through validated constructors.
/// </summary>
public static class DocumentDualRunTransportJson
{
    #region Variables and Constants

    private static readonly JsonSerializerOptions Options =
        CreateOptions();

    #endregion

    #region Methods Request

    public static byte[] SerializeRequestToUtf8Bytes(
        DocumentDualRunWorkerRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        return JsonSerializer
            .SerializeToUtf8Bytes(
                DocumentDualRunWorkerRequestJsonContract
                    .FromModel(
                        request),
                Options);
    }

    public static DocumentDualRunWorkerRequest DeserializeRequest(
        ReadOnlySpan<byte> utf8Json)
    {
        var contract =
            Deserialize<
                DocumentDualRunWorkerRequestJsonContract>(
                    utf8Json,
                    "Dual Run worker request");

        if (!string.Equals(
                contract.SchemaVersion,
                DocumentDualRunTransportSchema.RequestV1,
                StringComparison.Ordinal))
        {
            throw new UnsupportedDocumentDualRunTransportSchemaException(
                contract.SchemaVersion,
                DocumentDualRunTransportSchema.RequestV1);
        }

        return MapValidated(
            contract.ToModel,
            "Dual Run worker request JSON violates transport invariants.");
    }

    #endregion

    #region Methods Result

    public static byte[] SerializeResultToUtf8Bytes(
        DocumentDualRunWorkerResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        return JsonSerializer
            .SerializeToUtf8Bytes(
                DocumentDualRunWorkerResultJsonContract
                    .FromModel(
                        result),
                Options);
    }

    public static DocumentDualRunWorkerResult DeserializeResult(
        ReadOnlySpan<byte> utf8Json)
    {
        var contract =
            Deserialize<
                DocumentDualRunWorkerResultJsonContract>(
                    utf8Json,
                    "Dual Run worker result");

        if (!string.Equals(
                contract.SchemaVersion,
                DocumentDualRunTransportSchema.ResultV1,
                StringComparison.Ordinal))
        {
            throw new UnsupportedDocumentDualRunTransportSchemaException(
                contract.SchemaVersion,
                DocumentDualRunTransportSchema.ResultV1);
        }

        return MapValidated(
            contract.ToModel,
            "Dual Run worker result JSON violates transport invariants.");
    }

    #endregion

    #region Methods Serialization

    private static T Deserialize<T>(
        ReadOnlySpan<byte> utf8Json,
        string description)
        where T : class
    {
        if (utf8Json.IsEmpty)
        {
            throw new JsonException(
                $"{description} JSON cannot be empty.");
        }

        return JsonSerializer
                   .Deserialize<T>(
                       utf8Json,
                       Options) ??
               throw new JsonException(
                   $"{description} JSON must contain an object.");
    }

    private static T MapValidated<T>(
        Func<T> map,
        string message)
    {
        try
        {
            return map();
        }
        catch (UnsupportedDocumentDualRunTransportSchemaException)
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
                  FormatException)
        {
            throw new JsonException(
                message,
                exception);
        }
    }

    private static JsonSerializerOptions CreateOptions() =>
        new()
        {
            AllowDuplicateProperties =
                false,
            AllowTrailingCommas =
                false,
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull,
            MaxDepth =
                32,
            NumberHandling =
                JsonNumberHandling.Strict,
            PropertyNameCaseInsensitive =
                false,
            ReadCommentHandling =
                JsonCommentHandling.Disallow,
            RespectNullableAnnotations =
                true,
            UnmappedMemberHandling =
                JsonUnmappedMemberHandling.Disallow,
            WriteIndented =
                false
        };

    #endregion
}
