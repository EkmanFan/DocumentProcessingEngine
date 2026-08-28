using System.Net;
using System.Net.Http.Headers;
using DocumentProcessing.Manager.Blazor.ManagerApi;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerResultClientTests
{
    #region Tests

    [Fact]
    public async Task OpenResultAsync_StreamsExactAuthenticatedResult()
    {
        var expected =
            "{\"schemaVersion\":\"document-processing-result.v1\"}"u8.ToArray();

        var handler =
            new ResultHandler(
                HttpStatusCode.OK,
                expected);

        using var httpClient =
            CreateClient(
                handler);

        await using var result =
            await new ManagerResultClient(
                    httpClient)
                .OpenResultAsync(
                    "manager-result:abc123");

        Assert.NotNull(
            result);

        Assert.Equal(
            HttpMethod.Get,
            handler.Method);

        Assert.Equal(
            "http://manager.local/api/manager/results/manager-result%3Aabc123",
            handler.RequestUri?.AbsoluteUri);

        Assert.Equal(
            "application/vnd.document-processing-result+json",
            result.MediaType);

        Assert.Equal(
            expected.LongLength,
            result.ContentLength);

        await using var captured =
            new MemoryStream();

        await result.Content
            .CopyToAsync(
                captured);

        Assert.Equal(
            expected,
            captured.ToArray());
    }

    [Fact]
    public async Task OpenResultAsync_ReturnsNullWhenRetainedResultIsMissing()
    {
        using var httpClient =
            CreateClient(
                new ResultHandler(
                    HttpStatusCode.NotFound,
                    []));

        Assert.Null(
            await new ManagerResultClient(
                    httpClient)
                .OpenResultAsync(
                    "manager-result:missing"));
    }

    [Fact]
    public async Task OpenResultAsync_RejectsInvalidManagerCredentials()
    {
        using var httpClient =
            CreateClient(
                new ResultHandler(
                    HttpStatusCode.Unauthorized,
                    []));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await new ManagerResultClient(
                        httpClient)
                    .OpenResultAsync(
                        "manager-result:protected"));
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

    private sealed class ResultHandler(
        HttpStatusCode statusCode,
        byte[] content)
        : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method =
                request.Method;

            RequestUri =
                request.RequestUri;

            var responseContent =
                new ByteArrayContent(
                    content);

            responseContent.Headers.ContentType =
                new MediaTypeHeaderValue(
                    "application/vnd.document-processing-result+json");

            return Task.FromResult(
                new HttpResponseMessage(
                    statusCode)
                {
                    Content =
                        responseContent
                });
        }
    }

    #endregion
}
