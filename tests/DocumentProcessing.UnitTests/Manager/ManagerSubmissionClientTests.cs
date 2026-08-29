using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocumentProcessing.Manager.Blazor.Components.Workshop;
using DocumentProcessing.Manager.Blazor.ManagerApi;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerSubmissionClientTests
{
    #region Tests

    [Theory]
    [InlineData(
        "document.pdf",
        "document.pdf",
        (int)ManagerDocumentSubmissionBehavior.Shelve,
        "shelve")]
    [InlineData(
        "théologie.pdf",
        null,
        (int)ManagerDocumentSubmissionBehavior.Run,
        "run")]
    public async Task SubmitDocumentAsync_StreamsExactContentWithCompatibleFileNameHeaders(
        string fileName,
        string? expectedLegacyFileName,
        int submissionBehaviorValue,
        string expectedDispatchValue)
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
                        "manager-blazor",
                        (ManagerDocumentSubmissionBehavior)submissionBehaviorValue));

        Assert.Equal(
            HttpMethod.Put,
            handler.Method);

        Assert.Equal(
            $"http://manager.local/api/manager/submissions/{submissionId:D}?dispatch={expectedDispatchValue}",
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
                                "manager-blazor",
                                ManagerDocumentSubmissionBehavior.Shelve)));

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

    [Fact]
    public async Task SubmitDocumentAsync_SendsRequestedPhysicalPageRanges()
    {
        var submissionId =
            Guid.NewGuid();

        var handler =
            new RecordingSubmissionHandler(
                new ManagerDocumentSubmissionResult(
                    submissionId,
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    1,
                    "document.pdf",
                    [Guid.NewGuid(), Guid.NewGuid()],
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
                [1],
                writable:
                    false);

        await new ManagerSubmissionClient(
                httpClient)
            .SubmitDocumentAsync(
                new ManagerDocumentSubmissionRequest(
                    submissionId,
                    content,
                    1,
                    "document.pdf",
                    "application/pdf",
                    "manager-blazor",
                    ManagerDocumentSubmissionBehavior.Shelve,
                    [
                        new ManagerPageRangeRequest(1, 10, "Chapter one"),
                        new ManagerPageRangeRequest(11, 20, "Deuxième chapitre")
                    ]));

        var ranges =
            JsonSerializer.Deserialize<ManagerPageRangeRequest[]>(
                Assert.IsType<string>(
                    handler.PageRanges));

        Assert.Collection(
            Assert.IsType<ManagerPageRangeRequest[]>(
                ranges),
            range =>
                Assert.Equal(
                    1,
                    range.StartPhysicalPageNumber),
            range =>
                Assert.Equal(
                    "Deuxième chapitre",
                    range.Title));
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

        public string? PageRanges { get; private set; }

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

            PageRanges =
                request.Headers.TryGetValues(
                    "X-Processing-Page-Ranges",
                    out var pageRanges)
                    ? Encoding.UTF8.GetString(
                        Convert.FromBase64String(
                            Assert.Single(
                                pageRanges)))
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
