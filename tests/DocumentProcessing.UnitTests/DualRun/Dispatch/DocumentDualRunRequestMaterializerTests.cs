using System.Security.Cryptography;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.DualRun.Dispatch;
using DocumentProcessing.Engine.DualRun.Isolation;

namespace DocumentProcessing.UnitTests.DualRun.Dispatch;

public sealed class DocumentDualRunRequestMaterializerTests
{
    #region Variables and Constants

    private const string SelectedSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const string ProjectionSha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    #endregion

    #region Methods Materialization

    [Fact]
    public async Task CreateAsync_WritesStrictRequestJson_AndTransfersSnapshotOwnership()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var sourceBytes =
            "request-materialization-source"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        var jobId =
            Guid.NewGuid();

        var snapshotFactory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        var snapshot =
            await snapshotFactory
                .CreateAsync(
                    jobId,
                    source,
                    Sha256(
                        sourceBytes),
                    sourceBytes.Length);

        var materializer =
            new DocumentDualRunRequestMaterializer();

        var request =
            Request(
                snapshot);

        var prepared =
            await materializer
                .CreateAsync(
                    snapshot,
                    request);

        var jobDirectory =
            prepared.JobDirectoryPath;

        Assert.True(
            File.Exists(
                prepared.RequestFilePath));

        Assert.Equal(
            DocumentDualRunTransportSchema
                .RequestFileName,
            Path.GetFileName(
                prepared.RequestFilePath));

        Assert.False(
            File.Exists(
                Path.Combine(
                    jobDirectory,
                    "request.json.partial")));

        var roundTrip =
            DocumentDualRunTransportJson
                .DeserializeRequest(
                    await File.ReadAllBytesAsync(
                        prepared.RequestFilePath));

        Assert.Equal(
            request.JobId,
            roundTrip.JobId);

        Assert.Equal(
            request.SourceSnapshotPath,
            roundTrip.SourceSnapshotPath);

        Assert.Equal(
            request.SourceDocumentSha256,
            roundTrip.SourceDocumentSha256);

        Assert.Equal(
            request.SourceByteLength,
            roundTrip.SourceByteLength);

        await prepared
            .DisposeAsync();

        Assert.False(
            Directory.Exists(
                jobDirectory));

        await prepared
            .DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_MismatchedJobId_FailsBeforeWritingRequest()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var sourceBytes =
            "job-id-mismatch"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        var snapshotFactory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        await using var snapshot =
            await snapshotFactory
                .CreateAsync(
                    Guid.NewGuid(),
                    source,
                    Sha256(
                        sourceBytes),
                    sourceBytes.Length);

        var mismatched =
            new DocumentDualRunWorkerRequest(
                Guid.NewGuid(),
                DocumentDualRunExecutionMode.PlanningOnly,
                "test-engine-v1",
                snapshot.SourceSnapshotPath,
                snapshot.SourceDocumentSha256,
                snapshot.SourceByteLength,
                DocumentFormatId.Pdf,
                [
                    Baseline()
                ]);

        var materializer =
            new DocumentDualRunRequestMaterializer();

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await materializer
                    .CreateAsync(
                        snapshot,
                        mismatched));

        Assert.False(
            File.Exists(
                Path.Combine(
                    snapshot.JobDirectoryPath,
                    DocumentDualRunTransportSchema
                        .RequestFileName)));

        Assert.True(
            File.Exists(
                snapshot.SourceSnapshotPath));
    }

    [Fact]
    public async Task CreateAsync_MismatchedSourcePath_FailsBeforeWritingRequest()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var sourceBytes =
            "source-path-mismatch"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        var snapshotFactory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        await using var snapshot =
            await snapshotFactory
                .CreateAsync(
                    Guid.NewGuid(),
                    source,
                    Sha256(
                        sourceBytes),
                    sourceBytes.Length);

        var otherPath =
            Path.Combine(
                scope.Path,
                "other-job",
                DocumentDualRunTransportSchema
                    .SourceSnapshotFileName);

        var mismatched =
            new DocumentDualRunWorkerRequest(
                snapshot.JobId,
                DocumentDualRunExecutionMode.PlanningOnly,
                "test-engine-v1",
                otherPath,
                snapshot.SourceDocumentSha256,
                snapshot.SourceByteLength,
                DocumentFormatId.Pdf,
                [
                    Baseline()
                ]);

        var materializer =
            new DocumentDualRunRequestMaterializer();

        await Assert.ThrowsAsync<ArgumentException>(
            async () =>
                await materializer
                    .CreateAsync(
                        snapshot,
                        mismatched));

        Assert.False(
            File.Exists(
                Path.Combine(
                    snapshot.JobDirectoryPath,
                    DocumentDualRunTransportSchema
                        .RequestFileName)));
    }

    [Fact]
    public async Task CreateAsync_PreCancelled_DoesNotWriteRequest()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var sourceBytes =
            "cancelled-request"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        var snapshotFactory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        await using var snapshot =
            await snapshotFactory
                .CreateAsync(
                    Guid.NewGuid(),
                    source,
                    Sha256(
                        sourceBytes),
                    sourceBytes.Length);

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        var materializer =
            new DocumentDualRunRequestMaterializer();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
                await materializer
                    .CreateAsync(
                        snapshot,
                        Request(
                            snapshot),
                        cancellation.Token));

        Assert.False(
            File.Exists(
                Path.Combine(
                    snapshot.JobDirectoryPath,
                    DocumentDualRunTransportSchema
                        .RequestFileName)));

        Assert.False(
            File.Exists(
                Path.Combine(
                    snapshot.JobDirectoryPath,
                    "request.json.partial")));

        Assert.True(
            File.Exists(
                snapshot.SourceSnapshotPath));
    }

    [Fact]
    public async Task CreateAsync_ExistingRequestFile_FailsWithoutTakingSnapshotOwnership()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var sourceBytes =
            "existing-request"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        var snapshotFactory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        await using var snapshot =
            await snapshotFactory
                .CreateAsync(
                    Guid.NewGuid(),
                    source,
                    Sha256(
                        sourceBytes),
                    sourceBytes.Length);

        var requestPath =
            Path.Combine(
                snapshot.JobDirectoryPath,
                DocumentDualRunTransportSchema
                    .RequestFileName);

        await File.WriteAllTextAsync(
            requestPath,
            "already-present");

        var materializer =
            new DocumentDualRunRequestMaterializer();

        await Assert.ThrowsAsync<IOException>(
            async () =>
                await materializer
                    .CreateAsync(
                        snapshot,
                        Request(
                            snapshot)));

        Assert.True(
            File.Exists(
                snapshot.SourceSnapshotPath));

        Assert.Equal(
            "already-present",
            await File.ReadAllTextAsync(
                requestPath));
    }

    [Fact]
    public async Task CreateAsync_OnUnix_RequestFileRemovesGroupAndOtherAccess()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope =
            new TemporaryDirectoryScope();

        var sourceBytes =
            "private-request"u8
                .ToArray();

        await using var source =
            new MemoryStream(
                sourceBytes,
                writable:
                    false);

        var snapshotFactory =
            new DocumentDualRunSourceSnapshotFactory(
                scope.Path);

        var snapshot =
            await snapshotFactory
                .CreateAsync(
                    Guid.NewGuid(),
                    source,
                    Sha256(
                        sourceBytes),
                    sourceBytes.Length);

        var materializer =
            new DocumentDualRunRequestMaterializer();

        await using var prepared =
            await materializer
                .CreateAsync(
                    snapshot,
                    Request(
                        snapshot));

        const UnixFileMode groupOrOther =
            UnixFileMode.GroupRead |
            UnixFileMode.GroupWrite |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherWrite |
            UnixFileMode.OtherExecute;

        var mode =
            new FileInfo(
                prepared.RequestFilePath)
                .UnixFileMode;

        Assert.Equal(
            0,
            (int)(
                mode &
                groupOrOther));
    }

    #endregion

    #region Methods Test Data

    private static DocumentDualRunWorkerRequest Request(
        DocumentDualRunSourceSnapshot snapshot) =>
        new(
            snapshot.JobId,
            DocumentDualRunExecutionMode.PlanningOnly,
            "test-engine-v1",
            snapshot.SourceSnapshotPath,
            snapshot.SourceDocumentSha256,
            snapshot.SourceByteLength,
            DocumentFormatId.Pdf,
            [
                Baseline()
            ],
            "sample.pdf",
            "application/pdf");

    private static DocumentDualRunAuthoritativePageBaseline Baseline() =>
        new(
            1,
            NativeTextStatus.Healthy,
            PageProcessingRoute.NativeOnly,
            SelectedSha,
            ProjectionSha,
            authoritativeTextElementCount:
                1,
            authoritativeReconciliationEvidenceCount:
                0);

    private static string Sha256(
        byte[] source) =>
        Convert
            .ToHexString(
                SHA256.HashData(
                    source))
            .ToLowerInvariant();

    #endregion

    #region Test Types

    private sealed class TemporaryDirectoryScope
        : IDisposable
    {
        #region ctor

        public TemporaryDirectoryScope()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"dpe-dual-run-request-test-{Guid.NewGuid():N}");
        }

        #endregion

        #region Properties

        public string Path { get; }

        #endregion

        #region Methods Lifecycle

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(
                        Path))
                {
                    Directory.Delete(
                        Path,
                        recursive:
                            true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        #endregion
    }

    #endregion
}
