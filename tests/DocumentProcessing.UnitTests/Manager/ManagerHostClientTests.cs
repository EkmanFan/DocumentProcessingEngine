using System.Net;
using System.Text.Json;
using DocumentProcessing.Manager.Blazor.ManagerApi;

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

    private sealed class RecordingHandler
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
                await request.Content!
                    .ReadAsStringAsync(
                        cancellationToken);

            return new HttpResponseMessage(
                HttpStatusCode.OK);
        }
    }

    #endregion
}
