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
    public async Task RetryFailedProcessingUnitAsync_SendsVersionedUnitCommand()
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
            .RetryFailedProcessingUnitAsync(
                unitId,
                expectedVersion:
                    9);

        Assert.Equal(
            HttpMethod.Post,
            handler.Method);

        Assert.Equal(
            $"http://manager.local/api/manager/queue/{unitId:D}/retry",
            handler.RequestUri?.AbsoluteUri);

        using var document =
            JsonDocument.Parse(
                handler.Content);

        Assert.Equal(
            9,
            document.RootElement
                .GetProperty(
                    "expectedVersion")
                .GetInt64());
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("hide")]
    public async Task AdministrativeUnitCommandAsync_SendsVersionedCommand(
        string operation)
    {
        var unitId = Guid.NewGuid();
        var handler = new RecordingHandler();
        using var client = CreateClient(handler);
        var managerClient = new ManagerHostClient(client);

        if (operation == "remove")
        {
            await managerClient.RemovePendingProcessingUnitAsync(unitId, expectedVersion: 14);
        }
        else
        {
            await managerClient.HideTerminalProcessingUnitAsync(unitId, expectedVersion: 14);
        }

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            operation == "remove"
                ? $"http://manager.local/api/manager/queue/{unitId:D}/remove"
                : $"http://manager.local/api/manager/history/{unitId:D}/hide",
            handler.RequestUri?.AbsoluteUri);

        using var document = JsonDocument.Parse(handler.Content);
        Assert.Equal(14, document.RootElement.GetProperty("expectedVersion").GetInt64());
    }

    [Fact]
    public async Task ClearPendingQueueAsync_SendsVersionedCommand()
    {
        var handler = new RecordingHandler();
        using var client = CreateClient(handler);

        await new ManagerHostClient(client).ClearPendingQueueAsync(expectedVersion: 18);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "http://manager.local/api/manager/queue/clear",
            handler.RequestUri?.AbsoluteUri);

        using var document = JsonDocument.Parse(handler.Content);
        Assert.Equal(18, document.RootElement.GetProperty("expectedVersion").GetInt64());
    }

    [Fact]
    public async Task GetSplitPreviewAsync_MapsNativeNavigationSuggestion()
    {
        var unitId =
            Guid.NewGuid();

        var submissionId =
            Guid.NewGuid();

        var handler =
            new RecordingHandler(
                $$"""
                {
                  "unitId": "{{unitId:D}}",
                  "submissionId": "{{submissionId:D}}",
                  "originalFileName": "outlined.pdf",
                  "axisKind": "physicalPages",
                  "physicalPageCount": 30,
                  "contentUnits": [],
                  "splitSuggested": false,
                  "suggestedRanges": [
                    {
                      "kind": "physicalPageRange",
                      "startPhysicalPageNumber": 1,
                      "endPhysicalPageNumber": 10,
                      "suggestedTitle": "Chapter 1"
                    },
                    {
                      "kind": "physicalPageRange",
                      "startPhysicalPageNumber": 11,
                      "endPhysicalPageNumber": 30,
                      "suggestedTitle": "Chapter 2"
                    }
                  ]
                }
                """);

        using var client =
            CreateClient(
                handler);

        var preview =
            await new ManagerHostClient(
                    client)
                .GetSplitPreviewAsync(
                    unitId);

        Assert.Equal(
            30,
            preview.PhysicalPageCount);

        Assert.Collection(
            preview.SuggestedRanges,
            range =>
            {
                Assert.Equal(
                    1,
                    range.StartPhysicalPageNumber);

                Assert.Equal(
                    10,
                    range.EndPhysicalPageNumber);

                Assert.Equal(
                    "Chapter 1",
                    range.SuggestedTitle);
            },
            range =>
            {
                Assert.Equal(
                    11,
                    range.StartPhysicalPageNumber);

                Assert.Equal(
                    30,
                    range.EndPhysicalPageNumber);

                Assert.Equal(
                    "Chapter 2",
                    range.SuggestedTitle);
            });
    }

    [Fact]
    public async Task GetSplitPreviewAsync_MapsStructuredContentUnitProposal()
    {
        var unitId =
            Guid.NewGuid();

        var submissionId =
            Guid.NewGuid();

        var handler =
            new RecordingHandler(
                $$"""
                {
                  "unitId": "{{unitId:D}}",
                  "submissionId": "{{submissionId:D}}",
                  "originalFileName": "book.epub",
                  "axisKind": "contentUnits",
                  "physicalPageCount": null,
                  "contentUnits": [
                    {
                      "contentUnitIndex": 0,
                      "contentUnitId": "OPS/front.xhtml",
                      "suggestedTitle": null
                    },
                    {
                      "contentUnitIndex": 1,
                      "contentUnitId": "OPS/chapter1.xhtml",
                      "suggestedTitle": "Chapter 1"
                    }
                  ],
                  "splitSuggested": true,
                  "suggestedRanges": [
                    {
                      "kind": "contentUnitRange",
                      "startContentUnitIndex": 0,
                      "startContentUnitId": "OPS/front.xhtml",
                      "endContentUnitIndex": 0,
                      "endContentUnitId": "OPS/front.xhtml",
                      "suggestedTitle": null
                    },
                    {
                      "kind": "contentUnitRange",
                      "startContentUnitIndex": 1,
                      "startContentUnitId": "OPS/chapter1.xhtml",
                      "endContentUnitIndex": 1,
                      "endContentUnitId": "OPS/chapter1.xhtml",
                      "suggestedTitle": "Chapter 1"
                    }
                  ]
                }
                """);

        using var client =
            CreateClient(
                handler);

        var preview =
            await new ManagerHostClient(
                    client)
                .GetSplitPreviewAsync(
                    unitId);

        Assert.Equal(
            "contentUnits",
            preview.AxisKind);

        Assert.Null(
            preview.PhysicalPageCount);

        Assert.Equal(
            "OPS/chapter1.xhtml",
            preview.ContentUnits[1].ContentUnitId);

        Assert.Equal(
            1,
            preview.SuggestedRanges[1].StartContentUnitIndex);
    }

    [Fact]
    public async Task SplitPendingUnitAsync_SendsStableContentUnitBoundaries()
    {
        var unitId =
            Guid.NewGuid();

        var handler =
            new RecordingHandler(
                """
                { "processingUnitIds": [] }
                """);

        using var client =
            CreateClient(
                handler);

        await new ManagerHostClient(
                client)
            .SplitPendingUnitAsync(
                unitId,
                expectedVersion:
                    12,
                [
                    new ManagerSplitRangeDraft.ContentUnitRange(
                        0,
                        "OPS/front.xhtml",
                        1,
                        "OPS/chapter1.xhtml",
                        "Part one"),
                    new ManagerSplitRangeDraft.ContentUnitRange(
                        2,
                        "OPS/chapter2.xhtml",
                        3,
                        "OPS/chapter3.xhtml",
                        "Part two")
                ],
                releaseAfterSplit:
                    true);

        using var document =
            JsonDocument.Parse(
                handler.Content);

        var root =
            document.RootElement;

        Assert.Equal(
            12,
            root.GetProperty(
                    "expectedVersion")
                .GetInt64());

        Assert.True(
            root.GetProperty(
                    "releaseAfterSplit")
                .GetBoolean());

        var first =
            root.GetProperty(
                    "ranges")
                .EnumerateArray()
                .First();

        Assert.Equal(
            "contentUnitRange",
            first.GetProperty(
                    "kind")
                .GetString());

        Assert.Equal(
            "OPS/front.xhtml",
            first.GetProperty(
                    "startContentUnitId")
                .GetString());
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
