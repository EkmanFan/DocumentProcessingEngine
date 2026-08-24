using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DocumentProcessing.Ocr.Adapters.PaddleOCR;

/// <summary>
/// Calls a self-hosted PaddleOCR General OCR basic-serving endpoint for one
/// raster image and returns provider-native recognition evidence.
///
/// This client owns PaddleOCR transport, protocol validation and technical
/// resource limits only.
/// </summary>
public sealed class PaddleOcrServingClient
{
    #region Variables and Constants

    public const long DefaultMaxInputBytes = 16L * 1024L * 1024L;
    public const long DefaultMaxResponseBytes = 16L * 1024L * 1024L;

    public static readonly TimeSpan DefaultRequestTimeout =
        TimeSpan.FromSeconds(60);

    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly TimeSpan _requestTimeout;
    private readonly long _maxInputBytes;
    private readonly long _maxResponseBytes;

    #endregion


    #region ctor

    public PaddleOcrServingClient(
        HttpClient httpClient,
        Uri endpoint,
        TimeSpan? requestTimeout = null,
        long maxInputBytes = DefaultMaxInputBytes,
        long maxResponseBytes = DefaultMaxResponseBytes)
    {
        _httpClient =
            httpClient ??
            throw new ArgumentNullException(
                nameof(httpClient));

        ArgumentNullException.ThrowIfNull(
            endpoint);

        if (!endpoint.IsAbsoluteUri ||
            (endpoint.Scheme != Uri.UriSchemeHttp &&
             endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "PaddleOCR endpoint must be an absolute HTTP or HTTPS URI.",
                nameof(endpoint));
        }

        _requestTimeout =
            requestTimeout ??
            DefaultRequestTimeout;

        if (_requestTimeout <= TimeSpan.Zero ||
            _requestTimeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestTimeout),
                _requestTimeout,
                "Request timeout must be finite and greater than zero.");
        }

        if (maxInputBytes <= 0 ||
            maxInputBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxInputBytes));
        }

        if (maxResponseBytes <= 0 ||
            maxResponseBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxResponseBytes));
        }

        if (_httpClient.Timeout !=
                Timeout.InfiniteTimeSpan &&
            _httpClient.Timeout <
                _requestTimeout)
        {
            throw new ArgumentException(
                "HttpClient.Timeout must be infinite or at least as long as " +
                "the PaddleOCR request timeout.",
                nameof(httpClient));
        }

        _endpoint =
            endpoint;

        _maxInputBytes =
            maxInputBytes;

        _maxResponseBytes =
            maxResponseBytes;
    }

    #endregion


    #region Methods

    public async ValueTask<PaddleOcrNativeResult> RecognizeAsync(
        Stream rasterRegion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            rasterRegion);

        if (!rasterRegion.CanRead)
        {
            throw new ArgumentException(
                "Raster region stream must be readable.",
                nameof(rasterRegion));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var imageBytes =
            await ReadBoundedAsync(
                    rasterRegion,
                    _maxInputBytes,
                    "Raster region",
                    cancellationToken)
                .ConfigureAwait(false);

        var payload =
            JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    file =
                        Convert.ToBase64String(
                            imageBytes),
                    fileType = 1,
                    useDocOrientationClassify = false,
                    useDocUnwarping = false,
                    useTextlineOrientation = false,
                    textRecScoreThresh = 0d,
                    visualize = false
                });

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                _endpoint)
            {
                Content =
                    new ByteArrayContent(
                        payload)
            };

        request.Content.Headers.ContentType =
            new MediaTypeHeaderValue(
                "application/json");

        using var timeoutSource =
            new CancellationTokenSource(
                _requestTimeout);

        using var linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutSource.Token);

        HttpResponseMessage response;

        try
        {
            response =
                await _httpClient
                    .SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        linkedSource.Token)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested &&
                  timeoutSource.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"PaddleOCR request exceeded {_requestTimeout}.",
                exception);
        }

        using (response)
        {
            byte[] responseBytes;

            try
            {
                responseBytes =
                    await ReadBoundedResponseAsync(
                            response,
                            _maxResponseBytes,
                            linkedSource.Token)
                        .ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested &&
                      timeoutSource.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"PaddleOCR response exceeded {_requestTimeout}.",
                    exception);
            }

            if (!response.IsSuccessStatusCode)
            {
                var serviceMessage =
                    TryReadServiceErrorMessage(
                        responseBytes);

                throw new HttpRequestException(
                    serviceMessage is null
                        ? $"PaddleOCR returned HTTP {(int)response.StatusCode}."
                        : $"PaddleOCR returned HTTP {(int)response.StatusCode}: " +
                          serviceMessage,
                    inner: null,
                    response.StatusCode);
            }

            return ParseSuccessfulResponse(
                responseBytes);
        }
    }

    private static PaddleOcrNativeResult ParseSuccessfulResponse(
        byte[] responseBytes)
    {
        try
        {
            using var document =
                JsonDocument.Parse(
                    responseBytes);

            var root =
                document.RootElement;

            if (root.ValueKind !=
                JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "PaddleOCR serving response root must be an object.");
            }

            var errorCode =
                ReadRequiredInt32(
                    root,
                    "errorCode");

            if (errorCode != 0)
            {
                var errorMessage =
                    TryReadString(
                        root,
                        "errorMsg") ??
                    "Unspecified service error.";

                throw new InvalidDataException(
                    $"PaddleOCR service error {errorCode}: {errorMessage}");
            }

            if (!root.TryGetProperty(
                    "result",
                    out var result) ||
                result.ValueKind !=
                    JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "PaddleOCR serving response has no valid result object.");
            }

            if (!result.TryGetProperty(
                    "ocrResults",
                    out var ocrResults) ||
                ocrResults.ValueKind !=
                    JsonValueKind.Array)
            {
                throw new InvalidDataException(
                    "PaddleOCR serving response has no ocrResults array.");
            }

            if (ocrResults.GetArrayLength() != 1)
            {
                throw new InvalidDataException(
                    "Single-image targeted OCR must return exactly one " +
                    "ocrResults item.");
            }

            var item =
                ocrResults[0];

            if (item.ValueKind !=
                    JsonValueKind.Object ||
                !item.TryGetProperty(
                    "prunedResult",
                    out var prunedResult) ||
                prunedResult.ValueKind !=
                    JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "PaddleOCR serving response has no valid prunedResult.");
            }

            return new PaddleOcrNativeResult(
                JsonSerializer.SerializeToUtf8Bytes(
                    prunedResult));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "PaddleOCR serving response is not valid JSON.",
                exception);
        }
    }




    private static int ReadRequiredInt32(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var value))
        {
            throw new InvalidDataException(
                $"PaddleOCR serving response has no valid {propertyName}.");
        }

        return value;
    }

    private static string? TryReadString(
        JsonElement root,
        string propertyName)
    {
        if (!root.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static string? TryReadServiceErrorMessage(
        byte[] responseBytes)
    {
        try
        {
            using var document =
                JsonDocument.Parse(responseBytes);

            return document.RootElement.ValueKind ==
                   JsonValueKind.Object
                ? TryReadString(
                    document.RootElement,
                    "errorMsg")
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<byte[]> ReadBoundedResponseAsync(
        HttpResponseMessage response,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var contentLength =
            response.Content.Headers.ContentLength;

        if (contentLength.HasValue &&
            contentLength.Value > maxBytes)
        {
            throw new InvalidDataException(
                $"PaddleOCR response exceeds the {maxBytes}-byte limit.");
        }

        await using var stream =
            await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

        return await ReadBoundedAsync(
                stream,
                maxBytes,
                "PaddleOCR response",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        long maxBytes,
        string subject,
        CancellationToken cancellationToken)
    {
        long? originalPosition =
            null;

        if (stream.CanSeek)
        {
            originalPosition =
                stream.Position;

            var remaining =
                stream.Length -
                stream.Position;

            if (remaining > maxBytes)
            {
                throw new InvalidDataException(
                    $"{subject} exceeds the {maxBytes}-byte limit.");
            }
        }

        try
        {
            await using var buffer =
                new MemoryStream();

            var chunk =
                new byte[81920];

            long total =
                0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var read =
                    await stream
                        .ReadAsync(
                            chunk.AsMemory(),
                            cancellationToken)
                        .ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                total +=
                    read;

                if (total > maxBytes)
                {
                    throw new InvalidDataException(
                        $"{subject} exceeds the {maxBytes}-byte limit.");
                }

                await buffer
                    .WriteAsync(
                        chunk.AsMemory(0, read),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return buffer.ToArray();
        }
        finally
        {
            if (originalPosition.HasValue)
            {
                stream.Position =
                    originalPosition.Value;
            }
        }
    }

    #endregion
}
