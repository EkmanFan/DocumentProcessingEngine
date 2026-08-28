using DocumentProcessing.Manager.Blazor.Configuration;
using DocumentProcessing.Manager.Blazor.Components.Workshop;
using DocumentProcessing.Manager.Blazor.ManagerApi;
using DocumentProcessing.Manager.Blazor.Workshop;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;

namespace DocumentProcessing.UnitTests.Manager;

public sealed class ManagerWorkshopUploadServiceTests
{
    #region Tests

    [Theory]
    [InlineData(
        "document.PDF",
        "application/pdf",
        (int)ManagerDocumentSubmissionBehavior.Shelve)]
    [InlineData(
        "document.epub",
        "application/epub+zip",
        (int)ManagerDocumentSubmissionBehavior.Run)]
    public async Task SubmitAsync_ValidatesAndStreamsSupportedDocument(
        string fileName,
        string expectedMediaType,
        int submissionBehaviorValue)
    {
        var source =
            "exact source"u8.ToArray();

        var managerClient =
            new RecordingManagerSubmissionClient();

        var file =
            new StubBrowserFile(
                fileName,
                source);

        var result =
            await new ManagerWorkshopUploadService(
                    managerClient,
                    CreateOptions(
                        maximumUploadBytes:
                            1024))
                .SubmitAsync(
                    file,
                    (ManagerDocumentSubmissionBehavior)submissionBehaviorValue);

        Assert.True(
            file.Opened);

        Assert.Equal(
            source,
            managerClient.SubmittedContent);

        Assert.Equal(
            expectedMediaType,
            managerClient.Request?.MediaType);

        Assert.Equal(
            fileName,
            managerClient.Request?.OriginalFileName);

        Assert.Equal(
            "manager-blazor",
            managerClient.Request?.SourceOrigin);

        Assert.Equal(
            (ManagerDocumentSubmissionBehavior)submissionBehaviorValue,
            managerClient.Request?.SubmissionBehavior);

        Assert.Equal(
            fileName,
            result.OriginalFileName);
    }

    [Theory]
    [InlineData(
        "document.txt",
        10,
        1024,
        (int)ManagerWorkshopUploadValidationFailure.UnsupportedFormat)]
    [InlineData(
        "document.pdf",
        0,
        1024,
        (int)ManagerWorkshopUploadValidationFailure.NoReadableContent)]
    [InlineData(
        "document.epub",
        1025,
        1024,
        (int)ManagerWorkshopUploadValidationFailure.TooLarge)]
    public async Task SubmitAsync_RejectsInvalidDocumentBeforeOpeningStream(
        string fileName,
        long sourceLength,
        long maximumUploadBytes,
        int expectedFailureValue)
    {
        var file =
            new StubBrowserFile(
                fileName,
                new byte[checked(
                    (int)sourceLength)],
                reportedSize:
                    sourceLength);

        var exception =
            await Assert.ThrowsAsync<ManagerWorkshopUploadValidationException>(
                async () =>
                    await new ManagerWorkshopUploadService(
                            new RecordingManagerSubmissionClient(),
                            CreateOptions(
                                maximumUploadBytes))
                        .SubmitAsync(
                            file,
                            ManagerDocumentSubmissionBehavior.Shelve));

        Assert.Equal(
            (ManagerWorkshopUploadValidationFailure)expectedFailureValue,
            exception.Failure);

        Assert.False(
            file.Opened);
    }

    [Fact]
    public void UploadOptions_KeepStreamingAndShortRequestTimeoutsDistinct()
    {
        var options =
            CreateOptions(
                maximumUploadBytes:
                    1024);

        Assert.Equal(
            TimeSpan.FromSeconds(
                30),
            options.RequestTimeout);

        Assert.Equal(
            TimeSpan.FromHours(
                1),
            options.SubmissionTimeout);

        Assert.Equal(
            TimeSpan.FromHours(
                1),
            options.ResultDownloadTimeout);
    }

    #endregion

    #region Methods

    private static ManagerApiOptions CreateOptions(
        long maximumUploadBytes)
    {
        var configuration =
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ManagerApi:BaseAddress"] =
                            "http://manager.local",
                        ["ManagerApi:ApiKey"] =
                            "manager-workshop-tests-api-key-2026",
                        ["ManagerApi:MaximumUploadBytes"] =
                            maximumUploadBytes.ToString(
                                System.Globalization.CultureInfo.InvariantCulture)
                    })
                .Build();

        return ManagerApiOptions.Load(
            configuration);
    }

    #endregion

    #region Test Doubles

    private sealed class RecordingManagerSubmissionClient
        : IManagerSubmissionClient
    {
        public ManagerDocumentSubmissionRequest? Request { get; private set; }

        public byte[] SubmittedContent { get; private set; } =
            [];

        public async ValueTask<ManagerDocumentSubmissionResult> SubmitDocumentAsync(
            ManagerDocumentSubmissionRequest request,
            CancellationToken cancellationToken = default)
        {
            Request =
                request;

            await using var captured =
                new MemoryStream();

            await request.Content
                .CopyToAsync(
                    captured,
                    cancellationToken);

            SubmittedContent =
                captured.ToArray();

            return new ManagerDocumentSubmissionResult(
                request.SubmissionId,
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                request.ContentLength,
                request.OriginalFileName,
                [Guid.NewGuid()],
                Created:
                    true);
        }
    }

    private sealed class StubBrowserFile(
        string name,
        byte[] content,
        long? reportedSize = null)
        : IBrowserFile
    {
        public string Name { get; } =
            name;

        public DateTimeOffset LastModified { get; } =
            DateTimeOffset.UtcNow;

        public long Size { get; } =
            reportedSize ??
            content.LongLength;

        public string ContentType { get; } =
            "application/octet-stream";

        public bool Opened { get; private set; }

        public Stream OpenReadStream(
            long maxAllowedSize = 512000,
            CancellationToken cancellationToken = default)
        {
            Opened =
                true;

            if (Size >
                maxAllowedSize)
            {
                throw new IOException(
                    "The source exceeds the allowed size.");
            }

            return new MemoryStream(
                content,
                writable:
                    false);
        }
    }

    #endregion
}
