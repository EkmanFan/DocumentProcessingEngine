using System.Net;
using System.Text;
using System.Text.Json;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Layout;
using DocumentProcessing.Core.Raster;
using DocumentProcessing.Engine.Ocr;
using DocumentProcessing.Engine.Raster;

namespace DocumentProcessing.UnitTests.Ocr;

public sealed class PaddleOcrServingClientTests
{
    [Fact]
    public async Task RecognizeAsync_TextRegion_MapsEvidenceAndSendsSafeFlags()
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
                        "http://127.0.0.1:8081/ocr",
                        request.RequestUri!.ToString());

                    var body =
                        await request.Content!
                            .ReadAsStringAsync(cancellationToken);

                    using var json =
                        JsonDocument.Parse(body);

                    var root =
                        json.RootElement;

                    Assert.Equal(
                        Convert.ToBase64String(imageBytes),
                        root.GetProperty("file").GetString());
                    Assert.Equal(
                        1,
                        root.GetProperty("fileType").GetInt32());
                    Assert.False(
                        root.GetProperty("useDocOrientationClassify").GetBoolean());
                    Assert.False(
                        root.GetProperty("useDocUnwarping").GetBoolean());
                    Assert.False(
                        root.GetProperty("useTextlineOrientation").GetBoolean());
                    Assert.Equal(
                        0d,
                        root.GetProperty("textRecScoreThresh").GetDouble());
                    Assert.False(
                        root.GetProperty("visualize").GetBoolean());

                    return JsonResponse(
                        """
                        {
                          "logId": "targeted-ocr-test",
                          "errorCode": 0,
                          "errorMsg": "Success",
                          "result": {
                            "ocrResults": [
                              {
                                "prunedResult": {
                                  "rec_texts": [
                                    "Imagine,",
                                    "for example"
                                  ],
                                  "rec_scores": [
                                    0.98,
                                    0.91
                                  ],
                                  "rec_boxes": [
                                    [0, 0, 100, 20],
                                    [5, 30, 120, 50]
                                  ]
                                },
                                "ocrImage": null,
                                "docPreprocessingImage": null,
                                "inputImage": null
                              }
                            ],
                            "dataInfo": {}
                          }
                        }
                        """);
                });

        using var httpClient =
            CreateHttpClient(handler);

        var client =
            CreateClient(httpClient);

        var source =
            TextObservation();

        var crop =
            RasterCropGeometry.FromNormalized(
                source.Bounds,
                1000,
                1000);

        await using var image =
            new MemoryStream(
                imageBytes,
                writable: false);

        var result =
            await client.RecognizeAsync(
                image,
                source,
                crop,
                pagePixelWidth: 1000,
                pagePixelHeight: 1000);

        Assert.Equal(
            PaddleOcrServingClient.BackendId,
            result.BackendId);
        Assert.Equal(
            ProfileId,
            result.ProfileId);
        Assert.Same(
            source,
            result.SourceLayoutObservation);

        Assert.Equal(
            2,
            result.TextObservations.Count);

        var first =
            result.TextObservations[0];

        Assert.Equal(
            "Imagine,",
            first.Text);
        Assert.Equal(
            0.98,
            first.Confidence,
            6);
        Assert.Equal(
            233,
            first.PhysicalPageNumber);
        Assert.Equal(
            source.ObservationSequence,
            first.SourceLayoutObservationSequence);

        Assert.Equal(
            0.10,
            first.Bounds.Left,
            6);
        Assert.Equal(
            0.20,
            first.Bounds.Top,
            6);
        Assert.Equal(
            0.20,
            first.Bounds.Right,
            6);
        Assert.Equal(
            0.22,
            first.Bounds.Bottom,
            6);
    }

    [Fact]
    public async Task RecognizeAsync_FigureRegion_FailsClosedBeforeHttp()
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
            CreateHttpClient(handler);

        var client =
            CreateClient(httpClient);

        var figure =
            new LayoutObservation(
                physicalPageNumber: 233,
                observationSequence: 4,
                readingOrder: 4,
                LayoutObservationKind.Figure,
                new NormalizedRectangle(
                    0.2,
                    0.4,
                    0.5,
                    0.8),
                rawLabel: "image");

        var crop =
            RasterCropGeometry.FromNormalized(
                figure.Bounds,
                1000,
                1000);

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await client
                    .RecognizeAsync(
                        image,
                        figure,
                        crop,
                        1000,
                        1000)
                    .AsTask());

        Assert.Equal(
            0,
            sendCount);
    }

    [Fact]
    public async Task RecognizeAsync_MismatchedCrop_FailsBeforeHttp()
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
            CreateHttpClient(handler);

        var client =
            CreateClient(httpClient);
        var source =
            TextObservation();

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await client
                    .RecognizeAsync(
                        image,
                        source,
                        new PixelRectangle(
                            0,
                            0,
                            10,
                            10),
                        1000,
                        1000)
                    .AsTask());

        Assert.Equal(
            0,
            sendCount);
    }

    [Fact]
    public async Task RecognizeAsync_MismatchedParallelArrays_Throws()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        JsonResponse(
                            """
                            {
                              "logId": "bad-arrays",
                              "errorCode": 0,
                              "errorMsg": "Success",
                              "result": {
                                "ocrResults": [
                                  {
                                    "prunedResult": {
                                      "rec_texts": ["one", "two"],
                                      "rec_scores": [0.9],
                                      "rec_boxes": [
                                        [0, 0, 10, 10],
                                        [0, 10, 10, 20]
                                      ]
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
        var source =
            TextObservation();
        var crop =
            RasterCropGeometry.FromNormalized(
                source.Bounds,
                1000,
                1000);

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
                await client
                    .RecognizeAsync(
                        image,
                        source,
                        crop,
                        1000,
                        1000)
                    .AsTask());
    }

    [Fact]
    public async Task RecognizeAsync_ServiceErrorCode_Throws()
    {
        var handler =
            new StubHttpMessageHandler(
                (_, _) =>
                    Task.FromResult(
                        JsonResponse(
                            """
                            {
                              "logId": "service-error",
                              "errorCode": 17,
                              "errorMsg": "OCR backend unavailable",
                              "result": {}
                            }
                            """)));

        using var httpClient =
            CreateHttpClient(handler);

        var client =
            CreateClient(httpClient);
        var source =
            TextObservation();
        var crop =
            RasterCropGeometry.FromNormalized(
                source.Bounds,
                1000,
                1000);

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                    await client
                        .RecognizeAsync(
                            image,
                            source,
                            crop,
                            1000,
                            1000)
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
            CreateHttpClient(handler);

        var client =
            CreateClient(httpClient);
        var source =
            TextObservation();
        var crop =
            RasterCropGeometry.FromNormalized(
                source.Bounds,
                1000,
                1000);

        await using var image =
            new MemoryStream(
                new byte[] { 9, 8, 7, 6, 5 },
                writable: false);

        image.Position =
            2;

        await client.RecognizeAsync(
            image,
            source,
            crop,
            1000,
            1000);

        Assert.Equal(
            2,
            image.Position);
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
            CreateHttpClient(handler);

        var client =
            new PaddleOcrServingClient(
                httpClient,
                Endpoint,
                ProfileId,
                requestTimeout: TimeSpan.FromMilliseconds(50));

        var source =
            TextObservation();
        var crop =
            RasterCropGeometry.FromNormalized(
                source.Bounds,
                1000,
                1000);

        await using var image =
            new MemoryStream(
                new byte[] { 1 },
                writable: false);

        await Assert.ThrowsAsync<TimeoutException>(
            async () =>
                await client
                    .RecognizeAsync(
                        image,
                        source,
                        crop,
                        1000,
                        1000)
                    .AsTask());
    }

    private static LayoutObservation TextObservation() =>
        new(
            physicalPageNumber: 233,
            observationSequence: 3,
            readingOrder: 3,
            LayoutObservationKind.Text,
            new NormalizedRectangle(
                0.10,
                0.20,
                0.40,
                0.30),
            rawLabel: "text");

    private static PaddleOcrServingClient CreateClient(
        HttpClient httpClient) =>
        new(
            httpClient,
            Endpoint,
            ProfileId);

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

    private static readonly Uri Endpoint =
        new("http://127.0.0.1:8081/ocr");

    private const string ProfileId =
        "paddleocr-3.7.0-ppocrv6-medium-cpu-v1";

    private const string SuccessfulEmptyResponse =
        """
        {
          "logId": "success",
          "errorCode": 0,
          "errorMsg": "Success",
          "result": {
            "ocrResults": [
              {
                "prunedResult": {
                  "rec_texts": [],
                  "rec_scores": [],
                  "rec_boxes": []
                },
                "ocrImage": null,
                "docPreprocessingImage": null,
                "inputImage": null
              }
            ],
            "dataInfo": {}
          }
        }
        """;

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
