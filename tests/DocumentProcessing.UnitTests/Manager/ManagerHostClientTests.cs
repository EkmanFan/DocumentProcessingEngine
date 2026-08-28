using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentProcessing.Manager.Blazor.Components.Workshop;
using DocumentProcessing.Manager.Blazor.ManagerApi;
using DocumentProcessing.Manager.Blazor.Workshop;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerHostClientTests
{
    #region Tests

    [Fact]
    public async Task ReorderQueueAsync_SendsCompleteVersionedOrder()
    {
        var first =
            Guid.NewGuid();

        var second =
            Guid.NewGuid();

        var handler =
            new RecordingHandler();

        using var client =
            CreateClient(
                handler);

        await new ManagerHostClient(
                client)
            .ReorderQueueAsync(
                expectedVersion:
                    12,
                [second, first]);

        Assert.Equal(
            HttpMethod.Put,
            handler.Method);

        Assert.Equal(
            "http://manager.local/api/manager/queue/order",
            handler.RequestUri?.AbsoluteUri);

        using var document =
            JsonDocument.Parse(
                handler.Content);

        Assert.Equal(
            12,
            document.RootElement
                .GetProperty(
                    "expectedVersion")
                .GetInt64());

        Assert.Equal(
            [second, first],
            document.RootElement
                .GetProperty(
                    "orderedPendingUnitIds")
                .EnumerateArray()
                .Select(
                    value =>
                        value.GetGuid()));
    }

    [Fact]
    public async Task ReleaseProcessingUnitAsync_SendsVersionedUnitCommand()
    {
        var unitId =
            Guid.NewGuid();

        var handler =
            new RecordingHandler();

        using var client =
            CreateClient(
                handler);

        await new ManagerHostClient(
                client)
            .ReleaseProcessingUnitAsync(
                unitId,
                expectedVersion:
                    7);

        Assert.Equal(
            HttpMethod.Post,
            handler.Method);

        Assert.Equal(
            $"http://manager.local/api/manager/queue/{unitId:D}/release",
            handler.RequestUri?.AbsoluteUri);

        using var document =
            JsonDocument.Parse(
                handler.Content);

        Assert.Equal(
            7,
            document.RootElement
                .GetProperty(
                    "expectedVersion")
                .GetInt64());
    }

    [Fact]
    public async Task GetSettingsAsync_MapsDurableManagerSettings()
    {
        var handler =
            new RecordingHandler(
                """
                {
                  "defaultSubmissionBehavior": "run",
                  "visualDestinationRoot": "/var/lib/dpengine/visuals",
                  "version": 4,
                  "completedRetentionDays": 45
                }
                """);

        using var client =
            CreateClient(
                handler);

        var settings =
            await new ManagerHostClient(
                    client)
                .GetSettingsAsync();

        Assert.Equal(
            ManagerDocumentSubmissionBehavior.Run,
            settings.DefaultSubmissionBehavior);

        Assert.Equal(
            "/var/lib/dpengine/visuals",
            settings.VisualDestinationRoot);

        Assert.Equal(
            4,
            settings.Version);

        Assert.Equal(
            45,
            settings.CompletedRetentionDays);
    }

    [Fact]
    public async Task UpdateSettingsAsync_SendsVersionedBehaviorAndPath()
    {
        var handler =
            new RecordingHandler(
                """
                {
                  "defaultSubmissionBehavior": "shelve",
                  "visualDestinationRoot": "/data/visuals",
                  "version": 9,
                  "completedRetentionDays": 90
                }
                """);

        using var client =
            CreateClient(
                handler);

        var settings =
            await new ManagerHostClient(
                    client)
                .UpdateSettingsAsync(
                    expectedVersion:
                        8,
                    ManagerDocumentSubmissionBehavior.Shelve,
                    " /data/visuals ",
                    completedRetentionDays:
                        90);

        Assert.Equal(
            HttpMethod.Put,
            handler.Method);

        Assert.Equal(
            "http://manager.local/api/manager/settings",
            handler.RequestUri?.AbsoluteUri);

        using var document =
            JsonDocument.Parse(
                handler.Content);

        Assert.Equal(
            8,
            document.RootElement
                .GetProperty(
                    "expectedVersion")
                .GetInt64());

        Assert.Equal(
            "shelve",
            document.RootElement
                .GetProperty(
                    "defaultSubmissionBehavior")
                .GetString());

        Assert.Equal(
            "/data/visuals",
            document.RootElement
                .GetProperty(
                    "visualDestinationRoot")
                .GetString());

        Assert.Equal(
            90,
            document.RootElement
                .GetProperty(
                    "completedRetentionDays")
                .GetInt32());

        Assert.Equal(
            9,
            settings.Version);
    }

    [Fact]
    public async Task SearchArchiveAsync_SendsBoundedFiltersAndMapsPage()
    {
        var unitId =
            Guid.NewGuid();

        var submissionId =
            Guid.NewGuid();

        var handler =
            new RecordingHandler(
                $$"""
                {
                  "totalCount": 51,
                  "offset": 50,
                  "limit": 50,
                  "items": [
                    {
                      "unitId": "{{unitId}}",
                      "submissionId": "{{submissionId}}",
                      "originalFileName": "Historical Theology.pdf",
                      "scope": {
                        "kind": "wholeDocument",
                        "startPhysicalPageNumber": null,
                        "endPhysicalPageNumber": null,
                        "title": null
                      },
                      "attemptNumber": 1,
                      "status": "succeeded",
                      "dispatchState": "ready",
                      "queuePosition": null,
                      "resultReference": "result-1",
                      "lastFailureCode": null,
                      "lastFailureMessage": null,
                      "updatedAtUtc": "2026-07-01T12:00:00Z"
                    }
                  ]
                }
                """);

        using var client =
            CreateClient(
                handler);

        var page =
            await new ManagerHostClient(
                    client)
                .SearchArchiveAsync(
                    new ManagerArchiveQuery(
                        "Historical Theology",
                        new DateTimeOffset(
                            2026,
                            1,
                            1,
                            0,
                            0,
                            0,
                            TimeSpan.Zero),
                        new DateTimeOffset(
                            2026,
                            8,
                            1,
                            0,
                            0,
                            0,
                            TimeSpan.Zero),
                        ManagerArchiveSort.TitleAscending,
                        offset:
                            50));

        Assert.Equal(
            HttpMethod.Get,
            handler.Method);

        Assert.Contains(
            "sort=titleAscending",
            handler.RequestUri?.Query);

        Assert.Contains(
            "title=Historical%20Theology",
            handler.RequestUri?.Query);

        Assert.Equal(
            51,
            page.TotalCount);

        Assert.Equal(
            unitId,
            Assert.Single(
                    page.Items)
                .UnitId);
    }

    #endregion

    #region Methods

    private static HttpClient CreateClient(
        HttpMessageHandler handler) =>
        new(
            handler)
        {
            BaseAddress =
                new Uri(
                    "http://manager.local/")
        };

    #endregion

    #region Test Doubles

    private sealed class RecordingHandler(
        string? responseContent = null)
        : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string Content { get; private set; } =
            string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method =
                request.Method;

            RequestUri =
                request.RequestUri;

            Content =
                request.Content is null
                    ? string.Empty
                    : await request.Content
                        .ReadAsStringAsync(
                            cancellationToken);

            var response =
                new HttpResponseMessage(
                    HttpStatusCode.OK);

            if (responseContent is not null)
            {
                response.Content =
                    JsonContent.Create(
                        JsonDocument.Parse(
                                responseContent)
                            .RootElement);
            }

            return response;
        }
    }

    #endregion
}
