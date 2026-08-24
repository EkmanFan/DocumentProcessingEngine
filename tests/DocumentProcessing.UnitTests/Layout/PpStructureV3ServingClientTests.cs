using System.Net;
using System.Text;
using System.Text.Json;
using DocumentProcessing.Layout.Adapters.PpStructureV3;

namespace DocumentProcessing.UnitTests.Layout;

public sealed class PpStructureV3ServingClientTests
{
    [Fact]
    public async Task AnalyzeAsync_RepresentativeResponse_ReturnsNativeResultAndSendsSafeFlags()
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
                        "http://127.0.0.1:8080/layout-parsing",
                        request.RequestUri!.ToString());

                    var body =
                        await request.Content!
                            .ReadAsStringAsync(
                                cancellationToken);

                    using var requestJson =
                        JsonDocument.Parse(
                            body);

                    var root =
                        requestJson.RootElement;

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

                    Assert.False(
                        root.GetProperty(
                                "useSealRecognition")
                            .GetBoolean());

                    Assert.False(
                        root.GetProperty(
                                "useTableRecognition")
                            .GetBoolean());

                    Assert.False(
                        root.GetProperty(
                                "useFormulaRecognition")
                            .GetBoolean());

                    Assert.False(
                        root.GetProperty(
                                "useChartRecognition")
                            .GetBoolean());

                    Assert.True(
                        root.GetProperty(
                                "useRegionDetection")
                            .GetBoolean());

                    Assert.False(
                        root.GetProperty(
                                "formatBlockContent")
                            .GetBoolean());

                    Assert.False(
                        root.GetProperty(
                                "visualize")
                            .GetBoolean());

                    Assert.False(
                        root.GetProperty(
                                "returnMarkdownImages")
                            .GetBoolean());

                    return JsonResponse(
                        SuccessfulSingleTextResponse);
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
            await client.AnalyzeAsync(
                image);

        using var resultJson =
            JsonDocument.Parse(
                nativeResult.PrunedResultJson);

        var parsingResults =
            resultJson.RootElement
                .GetProperty(
                    "parsing_res_list");

        Assert.Equal(
            1,
            parsingResults.GetArrayLength());

        Assert.Equal(
            "text",
            parsingResults[0]
                .GetProperty(
                    "block_label")
                .GetString());
    }

    [Fact]
    public async Task AnalyzeAsync_RestoresSeekableInputPosition()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        JsonResponse(
                            SuccessfulSingleTextResponse)));

        using var httpClient =
            CreateHttpClient(handler);

        var client =
            CreateClient(httpClient);

        await using var image =
            new MemoryStream(
                new byte[] { 9, 8, 7, 6, 5 },
                writable: false);

        image.Position = 2;

        await client.AnalyzeAsync(image);

        Assert.Equal(2, image.Position);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsOversizedInputBeforeSendingRequest()
    {
        var sendCount = 0;

        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                {
                    sendCount++;

                    return Task.FromResult(
                        JsonResponse(
                            SuccessfulSingleTextResponse));
                });

        using var httpClient =
            CreateHttpClient(handler);

        var client =
            new PpStructureV3ServingClient(
                httpClient,
                Endpoint,
                maxInputBytes: 2);

        await using var image =
            new MemoryStream(
                new byte[] { 1, 2, 3 },
                writable: false);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await client.AnalyzeAsync(image)
                    .AsTask());

        Assert.Equal(0, sendCount);
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsOversizedResponse()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        JsonResponse(
                            SuccessfulSingleTextResponse)));

        using var httpClient =
            CreateHttpClient(handler);

        var client =
            new PpStructureV3ServingClient(
                httpClient,
                Endpoint,
                maxResponseBytes: 32);

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await client.AnalyzeAsync(image)
                    .AsTask());
    }

    [Fact]
    public async Task AnalyzeAsync_ServiceErrorCode_ThrowsInvalidDataException()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        JsonResponse(
                            """
                            {
                              "logId": "error",
                              "errorCode": 17,
                              "errorMsg": "Layout backend unavailable",
                              "result": {}
                            }
                            """)));

        using var httpClient =
            CreateHttpClient(handler);

        var client =
            CreateClient(httpClient);

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await client.AnalyzeAsync(image)
                        .AsTask());

        Assert.Contains(
            "17",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "Layout backend unavailable",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeAsync_ImageResponseWithMultiplePages_FailsClosed()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        JsonResponse(
                            """
                            {
                              "logId": "multi",
                              "errorCode": 0,
                              "errorMsg": "Success",
                              "result": {
                                "layoutParsingResults": [
                                  {
                                    "prunedResult": {
                                      "parsing_res_list": []
                                    }
                                  },
                                  {
                                    "prunedResult": {
                                      "parsing_res_list": []
                                    }
                                  }
                                ],
                                "dataInfo": {}
                              }
                            }
                            """)));

        using var httpClient =
            CreateHttpClient(handler);

        var client =
            CreateClient(httpClient);

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await client.AnalyzeAsync(image)
                    .AsTask());
    }

    [Fact]
    public async Task AnalyzeAsync_RequestTimeout_ThrowsTimeoutException()
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
            CreateHttpClient(handler);

        var client =
            new PpStructureV3ServingClient(
                httpClient,
                Endpoint,
                requestTimeout: TimeSpan.FromMilliseconds(50));

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        await Assert.ThrowsAsync<TimeoutException>(
            async () =>
                await client.AnalyzeAsync(image)
                    .AsTask());
    }

    [Fact]
    public async Task AnalyzeAsync_CallerCancellation_RemainsCancellation()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        JsonResponse(
                            SuccessfulSingleTextResponse)));

        using var httpClient =
            CreateHttpClient(handler);

        var client =
            CreateClient(httpClient);

        using var cancellationSource =
            new CancellationTokenSource();

        cancellationSource.Cancel();

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await client.AnalyzeAsync(image, cancellationSource.Token)
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
                new PpStructureV3ServingClient(
                    httpClient,
                    new Uri(
                        "file:///tmp/layout-parsing")));
    }

    private static readonly Uri Endpoint =
        new("http://127.0.0.1:8080/layout-parsing");

    private const string SuccessfulSingleTextResponse =
        """
        {
          "logId": "success",
          "errorCode": 0,
          "errorMsg": "Success",
          "result": {
            "layoutParsingResults": [
              {
                "prunedResult": {
                  "parsing_res_list": [
                    {
                      "block_bbox": [10, 20, 90, 80],
                      "block_label": "text",
                      "block_content": "ignored"
                    }
                  ]
                },
                "markdown": {
                  "text": "",
                  "images": null,
                  "isStart": true,
                  "isEnd": true
                },
                "outputImages": null,
                "inputImage": null
              }
            ],
            "dataInfo": {}
          }
        }
        """;

    private static PpStructureV3ServingClient CreateClient(
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
}
