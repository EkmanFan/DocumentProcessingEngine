using System.Net;
using System.Net.Http.Json;
using DocumentProcessing.Manager.Blazor.ManagerApi;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerSubmissionClientTests
{
    #region Tests

    [Theory]
    [InlineData(
        "document.pdf",
        "document.pdf")]
    [InlineData(
        "théologie.pdf",
        null)]
    public async Task SubmitDocumentAsync_StreamsExactContentWithCompatibleFileNameHeaders(
        string fileName,
        string? expectedLegacyFileName)
    {
        var source =
            new byte[]
            {
                0,
                1,
                2,
                255
            };

        var submissionId =
            Guid.NewGuid();

        var processingUnitId =
            Guid.NewGuid();

        var handler =
            new RecordingSubmissionHandler(
                new ManagerDocumentSubmissionResult(
                    submissionId,
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    source.LongLength,
                    fileName,
                    [processingUnitId],
                    Created:
                        true));

        using var httpClient =
            new HttpClient(
                handler)
            {
                BaseAddress =
                    new Uri(
                        "http://manager.local/")
            };

        await using var content =
            new MemoryStream(
                source,
                writable:
                    false);

        var result =
            await new ManagerSubmissionClient(
                    httpClient)
                .SubmitDocumentAsync(
                    new ManagerDocumentSubmissionRequest(
                        submissionId,
                        content,
                        source.LongLength,
                        fileName,
                        "application/pdf",
                        "manager-blazor"));

        Assert.Equal(
            HttpMethod.Put,
            handler.Method);

        Assert.Equal(
            $"http://manager.local/api/manager/submissions/{submissionId:D}",
            handler.RequestUri?.AbsoluteUri);

        Assert.Equal(
            source,
            handler.Content);

        Assert.Equal(
            source.LongLength,
            handler.ContentLength);

        Assert.Equal(
            "application/pdf",
            handler.MediaType);

        Assert.Equal(
            fileName,
            handler.FileNameStar);

        Assert.Equal(
            expectedLegacyFileName,
            handler.LegacyFileName);

        Assert.True(
            handler.ExpectContinue);

        Assert.Equal(
            "manager-blazor",
            handler.SourceOrigin);

        Assert.Equal(
            submissionId,
            result.SubmissionId);

        Assert.Equal(
            processingUnitId,
            Assert.Single(
                result.ProcessingUnitIds));

        Assert.True(
            result.Created);
    }

    [Fact]
    public async Task SubmitDocumentAsync_PreservesManagerRejectionDetails()
    {
        using var httpClient =
            new HttpClient(
                new RejectingSubmissionHandler())
            {
                BaseAddress =
                    new Uri(
                        "http://manager.local/")
            };

        await using var content =
            new MemoryStream(
                "source"u8.ToArray(),
                writable:
                    false);

        var exception =
            await Assert.ThrowsAsync<ManagerSubmissionRejectedException>(
                async () =>
                    await new ManagerSubmissionClient(
                            httpClient)
                        .SubmitDocumentAsync(
                            new ManagerDocumentSubmissionRequest(
                                Guid.NewGuid(),
                                content,
                                content.Length,
                                "document.pdf",
                                "application/pdf",
                                "manager-blazor")));

        Assert.Equal(
            HttpStatusCode.BadRequest,
            exception.StatusCode);

        Assert.Equal(
            "manager.file_name_required",
            exception.ManagerCode);

        Assert.Equal(
            "A source filename is required.",
            exception.Message);
    }

    #endregion

    #region Test Doubles

    private sealed class RecordingSubmissionHandler(
        ManagerDocumentSubmissionResult response)
        : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public byte[] Content { get; private set; } =
            [];

        public long? ContentLength { get; private set; }

        public string? MediaType { get; private set; }

        public string? FileNameStar { get; private set; }

        public string? LegacyFileName { get; private set; }

        public bool? ExpectContinue { get; private set; }

        public string? SourceOrigin { get; private set; }

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
                    .ReadAsByteArrayAsync(
                        cancellationToken);

            ContentLength =
                request.Content.Headers.ContentLength;

            MediaType =
                request.Content.Headers.ContentType?.MediaType;

            FileNameStar =
                request.Content.Headers.ContentDisposition?.FileNameStar;

            LegacyFileName =
                request.Headers.TryGetValues(
                    "X-Document-File-Name",
                    out var legacyFileNames)
                    ? Assert.Single(
                        legacyFileNames)
                    : null;

            ExpectContinue =
                request.Headers.ExpectContinue;

            SourceOrigin =
                request.Headers.TryGetValues(
                    "X-Source-Origin",
                    out var values)
                    ? Assert.Single(
                        values)
                    : null;

            return new HttpResponseMessage(
                HttpStatusCode.Created)
            {
                Content =
                    JsonContent.Create(
                        response)
            };
        }
    }

    private sealed class RejectingSubmissionHandler
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.BadRequest)
                {
                    Content =
                        JsonContent.Create(
                            new ManagerApiErrorContract(
                                "manager.file_name_required",
                                "A source filename is required.",
                                Title:
                                    null,
                                Detail:
                                    null))
                });
    }

    #endregion
}
