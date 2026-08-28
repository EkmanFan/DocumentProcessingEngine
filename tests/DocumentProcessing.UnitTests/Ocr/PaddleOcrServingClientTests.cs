using System.Net;
using System.Text;
using System.Text.Json;
using DocumentProcessing.Ocr.Adapters.PaddleOCR;

namespace DocumentProcessing.UnitTests.Ocr;

public sealed class PaddleOcrServingClientTests
{
    #region Variables and Constants

    private static readonly Uri Endpoint =
        new("http://127.0.0.1:8081/ocr");

    private const string SuccessfulEmptyResponse =
        "{"
        + "\"logId\":\"success\","
        + "\"errorCode\":0,"
        + "\"errorMsg\":\"Success\","
        + "\"result\":{"
        + "\"ocrResults\":[{"
        + "\"prunedResult\":{"
        + "\"rec_texts\":[],"
        + "\"rec_scores\":[],"
        + "\"rec_boxes\":[]"
        + "}}],"
        + "\"dataInfo\":{}"
        + "}"
        + "}";

    #endregion


    #region Tests

    [Fact]
    public async Task RecognizeAsync_RepresentativeResponse_ReturnsNativeResultAndSendsSafeFlags()
    {
        var imageBytes =
            new byte[] { 1, 2, 3, 4 };

        var handler =
            new StubHttpMessageHandler(
                async (request, cancellationToken) =>
                {
                    Assert.Equal(
                        HttpMethod.Post,
                        request.Method);

                    Assert.Equal(
                        Endpoint,
                        request.RequestUri);

                    var body =
                        await request.Content!
                            .ReadAsStringAsync(
                                cancellationToken);

                    using var json =
                        JsonDocument.Parse(
                            body);

                    var root =
                        json.RootElement;

                    Assert.Equal(
                        Convert.ToBase64String(
                            imageBytes),
                        root.GetProperty(
                                "file")
                            .GetString());

                    Assert.Equal(
                        1,
                        root.GetProperty(
                                "fileType")
                            .GetInt32());

                    Assert.False(
                        root.GetProperty(
                                "useDocOrientationClassify")
                            .GetBoolean());

                    Assert.False(
                        root.GetProperty(
                                "useDocUnwarping")
                            .GetBoolean());

                    Assert.False(
                        root.GetProperty(
                                "useTextlineOrientation")
                            .GetBoolean());

                    Assert.Equal(
                        0d,
                        root.GetProperty(
                                "textRecScoreThresh")
                            .GetDouble());

                    Assert.False(
                        root.GetProperty(
                                "visualize")
                            .GetBoolean());

                    return JsonResponse(
                        "{"
                        + "\"logId\":\"success\","
                        + "\"errorCode\":0,"
                        + "\"errorMsg\":\"Success\","
                        + "\"result\":{"
                        + "\"ocrResults\":[{"
                        + "\"prunedResult\":{"
                        + "\"rec_texts\":[\"Imagine,\"],"
                        + "\"rec_scores\":[0.98],"
                        + "\"rec_boxes\":[[0,0,100,20]]"
                        + "}}],"
                        + "\"dataInfo\":{}"
                        + "}"
                        + "}");
                });

        using var httpClient =
            CreateHttpClient(
                handler);

        var client =
            CreateClient(
                httpClient);

        await using var image =
            new MemoryStream(
                imageBytes,
                writable: false);

        var nativeResult =
            await client.RecognizeAsync(
                image);

        using var nativeJson =
            JsonDocument.Parse(
                nativeResult.PrunedResultJson);

        Assert.Equal(
            "Imagine,",
            nativeJson.RootElement
                .GetProperty(
                    "rec_texts")[0]
                .GetString());
    }

    [Fact]
    public async Task RecognizeAsync_ServiceErrorCode_Throws()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        JsonResponse(
                            "{"
                            + "\"logId\":\"service-error\","
                            + "\"errorCode\":17,"
                            + "\"errorMsg\":\"OCR backend unavailable\","
                            + "\"result\":{}"
                            + "}")));

        using var httpClient =
            CreateHttpClient(
                handler);

        var client =
            CreateClient(
                httpClient);

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await client
                        .RecognizeAsync(
                            image)
                        .AsTask());

        Assert.Contains(
            "17",
            exception.Message,
            StringComparison.Ordinal);

        Assert.Contains(
            "OCR backend unavailable",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecognizeAsync_RestoresSeekableInputPosition()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        JsonResponse(
                            SuccessfulEmptyResponse)));

        using var httpClient =
            CreateHttpClient(
                handler);

        var client =
            CreateClient(
                httpClient);

        await using var image =
            new MemoryStream(
                new byte[] { 9, 8, 7, 6, 5 },
                writable: false);

        image.Position =
            2;

        await client.RecognizeAsync(
            image);

        Assert.Equal(
            2,
            image.Position);
    }

    [Fact]
    public async Task RecognizeAsync_EnsuresProviderBeforeSendingRequest()
    {
        var providerReady =
            false;

        using var httpClient =
            CreateHttpClient(
                new StubHttpMessageHandler(
                    (_, _) =>
                    {
                        Assert.True(
                            providerReady);

                        return Task.FromResult(
                            JsonResponse(
                                SuccessfulEmptyResponse));
                    }));

        var client =
            new PaddleOcrServingClient(
                httpClient,
                Endpoint,
                ensureAvailable:
                    _ =>
                    {
                        providerReady =
                            true;

                        return ValueTask.CompletedTask;
                    });

        await using var image =
            new MemoryStream(
                [1],
                writable:
                    false);

        await client.RecognizeAsync(
            image);
    }

    [Fact]
    public async Task RecognizeAsync_TransportFailureReportsProviderUnavailable()
    {
        var unavailableReports =
            0;

        using var httpClient =
            CreateHttpClient(
                new StubHttpMessageHandler(
                    (_, _) =>
                        throw new HttpRequestException(
                            "Connection refused.")));

        var client =
            new PaddleOcrServingClient(
                httpClient,
                Endpoint,
                reportUnavailable:
                    () =>
                        unavailableReports++);

        await using var image =
            new MemoryStream(
                [1],
                writable:
                    false);

        await Assert.ThrowsAsync<HttpRequestException>(
            async () =>
                await client.RecognizeAsync(
                        image)
                    .AsTask());

        Assert.Equal(
            1,
            unavailableReports);
    }

    [Fact]
    public async Task RecognizeAsync_RequestTimeout_ThrowsTimeoutException()
    {
        var handler =
            new StubHttpMessageHandler(
                async (_, cancellationToken) =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "Unreachable.");
                });

        using var httpClient =
            CreateHttpClient(
                handler);

        var client =
            new PaddleOcrServingClient(
                httpClient,
                Endpoint,
                requestTimeout:
                    TimeSpan.FromMilliseconds(
                        50));

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        await Assert.ThrowsAsync<TimeoutException>(
            async () =>
                await client
                    .RecognizeAsync(
                        image)
                    .AsTask());
    }

    [Fact]
    public async Task RecognizeAsync_RejectsOversizedInputBeforeSendingRequest()
    {
        var sendCount =
            0;

        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                {
                    sendCount++;

                    return Task.FromResult(
                        JsonResponse(
                            SuccessfulEmptyResponse));
                });

        using var httpClient =
            CreateHttpClient(
                handler);

        var client =
            new PaddleOcrServingClient(
                httpClient,
                Endpoint,
                maxInputBytes:
                    2);

        await using var image =
            new MemoryStream(
                new byte[] { 1, 2, 3 },
                writable: false);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await client
                    .RecognizeAsync(
                        image)
                    .AsTask());

        Assert.Equal(
            0,
            sendCount);
    }

    [Fact]
    public async Task RecognizeAsync_RejectsOversizedResponse()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        JsonResponse(
                            SuccessfulEmptyResponse)));

        using var httpClient =
            CreateHttpClient(
                handler);

        var client =
            new PaddleOcrServingClient(
                httpClient,
                Endpoint,
                maxResponseBytes:
                    32);

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await client
                    .RecognizeAsync(
                        image)
                    .AsTask());
    }

    [Fact]
    public async Task RecognizeAsync_CallerCancellation_RemainsCancellation()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        JsonResponse(
                            SuccessfulEmptyResponse)));

        using var httpClient =
            CreateHttpClient(
                handler);

        var client =
            CreateClient(
                httpClient);

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await client
                    .RecognizeAsync(
                        image,
                        cancellationSource.Token)
                    .AsTask());
    }

    [Fact]
    public void Constructor_RejectsNonHttpEndpoint()
    {
        using var httpClient =
            CreateHttpClient(
                new StubHttpMessageHandler(
                    (_, _) =>
                        throw new InvalidOperationException(
                            "No request expected.")));

        Assert.Throws<ArgumentException>(
            () =>
                new PaddleOcrServingClient(
                    httpClient,
                    new Uri(
                        "file:///tmp/ocr")));
    }

    #endregion


    #region Methods

    private static PaddleOcrServingClient CreateClient(
        HttpClient httpClient) =>
        new(
            httpClient,
            Endpoint);

    private static HttpClient CreateHttpClient(
        HttpMessageHandler handler) =>
        new(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

    private static HttpResponseMessage JsonResponse(
        string json) =>
        new(HttpStatusCode.OK)
        {
            Content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
            sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            sendAsync(
                request,
                cancellationToken);
    }

    #endregion
}
