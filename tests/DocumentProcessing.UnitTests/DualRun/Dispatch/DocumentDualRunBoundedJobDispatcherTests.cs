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

public sealed class DocumentDualRunBoundedJobDispatcherTests
{
    #region Variables and Constants

    private const string SelectedSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const string ProjectionSha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    #endregion

    #region Methods Construction

    [Fact]
    public void Constructor_NonPositiveCapacity_FailsClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new DocumentDualRunBoundedJobDispatcher(
                    0));
    }

    #endregion

    #region Methods Dispatch

    [Fact]
    public async Task TryDispatch_Enqueued_TransfersOwnershipToQueueThenConsumer()
    {
        using var scope =
            new TemporaryDirectoryScope();

        await using var dispatcher =
            new DocumentDualRunBoundedJobDispatcher(
                capacity:
                    1);

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                "enqueued");

        var jobDirectory =
            job.JobDirectoryPath;

        Assert.Equal(
            DocumentDualRunDispatchOutcome.Enqueued,
            dispatcher.TryDispatch(
                job));

        Assert.Equal(
            1,
            dispatcher.Count);

        Assert.True(
            dispatcher.TryTake(
                out var taken));

        Assert.Same(
            job,
            taken);

        Assert.Equal(
            0,
            dispatcher.Count);

        Assert.True(
            Directory.Exists(
                jobDirectory));

        await taken!
            .DisposeAsync();

        Assert.False(
            Directory.Exists(
                jobDirectory));
    }

    [Fact]
    public async Task TryDispatch_QueueFull_IsImmediateAndCallerRetainsRejectedJob()
    {
        using var firstScope =
            new TemporaryDirectoryScope();

        using var secondScope =
            new TemporaryDirectoryScope();

        await using var dispatcher =
            new DocumentDualRunBoundedJobDispatcher(
                capacity:
                    1);

        var accepted =
            await CreatePreparedJobAsync(
                firstScope.Path,
                "accepted");

        var rejected =
            await CreatePreparedJobAsync(
                secondScope.Path,
                "rejected");

        var rejectedDirectory =
            rejected.JobDirectoryPath;

        Assert.Equal(
            DocumentDualRunDispatchOutcome.Enqueued,
            dispatcher.TryDispatch(
                accepted));

        Assert.Equal(
            DocumentDualRunDispatchOutcome.QueueFull,
            dispatcher.TryDispatch(
                rejected));

        Assert.Equal(
            1,
            dispatcher.Count);

        Assert.True(
            Directory.Exists(
                rejectedDirectory));

        await rejected
            .DisposeAsync();

        Assert.False(
            Directory.Exists(
                rejectedDirectory));
    }

    [Fact]
    public async Task DisposeAsync_StopsDispatcherAndDrainsOwnedJobs()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var dispatcher =
            new DocumentDualRunBoundedJobDispatcher(
                capacity:
                    2);

        var first =
            await CreatePreparedJobAsync(
                scope.Path,
                "first");

        var second =
            await CreatePreparedJobAsync(
                scope.Path,
                "second");

        var firstDirectory =
            first.JobDirectoryPath;

        var secondDirectory =
            second.JobDirectoryPath;

        Assert.Equal(
            DocumentDualRunDispatchOutcome.Enqueued,
            dispatcher.TryDispatch(
                first));

        Assert.Equal(
            DocumentDualRunDispatchOutcome.Enqueued,
            dispatcher.TryDispatch(
                second));

        await dispatcher
            .DisposeAsync();

        Assert.True(
            dispatcher.IsStopped);

        Assert.Equal(
            0,
            dispatcher.Count);

        Assert.False(
            Directory.Exists(
                firstDirectory));

        Assert.False(
            Directory.Exists(
                secondDirectory));

        await dispatcher
            .DisposeAsync();
    }

    [Fact]
    public async Task TryDispatch_AfterStop_ReturnsStoppedAndCallerRetainsJob()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var dispatcher =
            new DocumentDualRunBoundedJobDispatcher(
                capacity:
                    1);

        await dispatcher
            .DisposeAsync();

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                "after-stop");

        var jobDirectory =
            job.JobDirectoryPath;

        Assert.Equal(
            DocumentDualRunDispatchOutcome.Stopped,
            dispatcher.TryDispatch(
                job));

        Assert.True(
            Directory.Exists(
                jobDirectory));

        await job
            .DisposeAsync();

        Assert.False(
            Directory.Exists(
                jobDirectory));
    }

    [Fact]
    public async Task TryTake_EmptyQueue_IsNonBlocking()
    {
        await using var dispatcher =
            new DocumentDualRunBoundedJobDispatcher(
                capacity:
                    1);

        Assert.False(
            dispatcher.TryTake(
                out var job));

        Assert.Null(
            job);
    }

    #endregion

    #region Methods Test Data

    private static async Task<DocumentDualRunPreparedJob> CreatePreparedJobAsync(
        string spoolRoot,
        string payload)
    {
        var bytes =
            System.Text.Encoding.UTF8
                .GetBytes(
                    payload);

        var snapshotFactory =
            new DocumentDualRunSourceSnapshotFactory(
                spoolRoot);

        await using var source =
            new MemoryStream(
                bytes,
                writable:
                    false);

        var snapshot =
            await snapshotFactory
                .CreateAsync(
                    Guid.NewGuid(),
                    source,
                    Sha256(
                        bytes),
                    bytes.Length);

        var request =
            new DocumentDualRunWorkerRequest(
                snapshot.JobId,
                DocumentDualRunExecutionMode.PlanningOnly,
                "test-engine-v1",
                snapshot.SourceSnapshotPath,
                snapshot.SourceDocumentSha256,
                snapshot.SourceByteLength,
                DocumentFormatId.Pdf,
                [
                    new DocumentDualRunAuthoritativePageBaseline(
                        1,
                        NativeTextStatus.Healthy,
                        PageProcessingRoute.NativeOnly,
                        SelectedSha,
                        ProjectionSha,
                        authoritativeTextElementCount:
                            1,
                        authoritativeReconciliationEvidenceCount:
                            0)
                ]);

        try
        {
            return await new DocumentDualRunRequestMaterializer()
                .CreateAsync(
                    snapshot,
                    request);
        }
        catch
        {
            await snapshot
                .DisposeAsync();

            throw;
        }
    }

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
                    $"dpe-dual-run-dispatch-test-{Guid.NewGuid():N}");
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
