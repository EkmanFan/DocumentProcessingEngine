using System.Security.Cryptography;
using System.Text;
using DocumentProcessing.Core.Documents;
using DocumentProcessing.Core.DualRun;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Core.Extraction;
using DocumentProcessing.Core.Planning;
using DocumentProcessing.Core.Reconciliation;
using DocumentProcessing.Engine.DualRun.Dispatch;
using DocumentProcessing.Engine.DualRun.Isolation;
using DocumentProcessing.Engine.DualRun.Submission;

namespace DocumentProcessing.UnitTests.DualRun.Submission;

public sealed class DocumentDualRunSubmissionCoordinatorTests
{
    #region Variables and Constants

    private const string SelectedSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const string ProjectionSha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    #endregion

    #region Methods Selection Before Work

    [Fact]
    public async Task SubmitAsync_Disabled_DoesNotResolveDispatcherOrBuildSelectedEnvelope()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var dispatcherFactoryCalls =
            0;

        var selectedFactoryCalls =
            0;

        var coordinator =
            Coordinator(
                scope.Path,
                () =>
                {
                    dispatcherFactoryCalls++;

                    throw new InvalidOperationException(
                        "Dispatcher must not be resolved.");
                });

        var result =
            await coordinator
                .SubmitAsync(
                    new DocumentDualRunProfileSnapshot(
                        DocumentDualRunProfile.Disabled),
                    Sha256(
                        "disabled"u8.ToArray()),
                    () =>
                    {
                        selectedFactoryCalls++;

                        throw new InvalidOperationException(
                            "Selected envelope must not be built.");
                    });

        Assert.Equal(
            DocumentDualRunSubmissionStatus.NotSelected,
            result.Status);

        Assert.Equal(
            0,
            dispatcherFactoryCalls);

        Assert.Equal(
            0,
            selectedFactoryCalls);

        Assert.False(
            Directory.Exists(
                scope.Path));
    }

    [Fact]
    public async Task SubmitAsync_SampledZero_DoesNotResolveDispatcherOrBuildSelectedEnvelope()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var dispatcherFactoryCalls =
            0;

        var selectedFactoryCalls =
            0;

        var coordinator =
            Coordinator(
                scope.Path,
                () =>
                {
                    dispatcherFactoryCalls++;

                    throw new InvalidOperationException(
                        "Dispatcher must not be resolved.");
                });

        var result =
            await coordinator
                .SubmitAsync(
                    new DocumentDualRunProfileSnapshot(
                        DocumentDualRunProfile.Sampled,
                        sampledBasisPoints:
                            0),
                    Sha256(
                        "sampled-zero"u8.ToArray()),
                    () =>
                    {
                        selectedFactoryCalls++;

                        throw new InvalidOperationException(
                            "Selected envelope must not be built.");
                    });

        Assert.Equal(
            DocumentDualRunSubmissionStatus.NotSelected,
            result.Status);

        Assert.Equal(
            0,
            dispatcherFactoryCalls);

        Assert.Equal(
            0,
            selectedFactoryCalls);

        Assert.False(
            Directory.Exists(
                scope.Path));
    }

    [Fact]
    public async Task SubmitAsync_PreCancelledSelectedDocument_DoesNoDispatcherOrEnvelopeWork()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var dispatcherFactoryCalls =
            0;

        var selectedFactoryCalls =
            0;

        var coordinator =
            Coordinator(
                scope.Path,
                () =>
                {
                    dispatcherFactoryCalls++;

                    throw new InvalidOperationException(
                        "Dispatcher must not be resolved.");
                });

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        var result =
            await coordinator
                .SubmitAsync(
                    new DocumentDualRunProfileSnapshot(
                        DocumentDualRunProfile.Full),
                    Sha256(
                        "cancelled"u8.ToArray()),
                    () =>
                    {
                        selectedFactoryCalls++;

                        throw new InvalidOperationException(
                            "Selected envelope must not be built.");
                    },
                    cancellation.Token);

        Assert.Equal(
            DocumentDualRunSubmissionStatus.Cancelled,
            result.Status);

        Assert.Equal(
            0,
            dispatcherFactoryCalls);

        Assert.Equal(
            0,
            selectedFactoryCalls);

        Assert.False(
            Directory.Exists(
                scope.Path));
    }

    #endregion

    #region Methods Successful Submission

    [Fact]
    public async Task SubmitAsync_PlanningOnly_EnqueuesPlanningOnlyJob()
    {
        using var scope =
            new TemporaryDirectoryScope();

        await using var dispatcher =
            new DocumentDualRunBoundedJobDispatcher(
                capacity:
                    1);

        var bytes =
            "planning-only"u8
                .ToArray();

        await using var sourceStream =
            new MemoryStream(
                bytes,
                writable:
                    false);

        var context =
            SelectedSubmission(
                sourceStream,
                bytes);

        var coordinator =
            Coordinator(
                scope.Path,
                () =>
                    dispatcher);

        var result =
            await coordinator
                .SubmitAsync(
                    new DocumentDualRunProfileSnapshot(
                        DocumentDualRunProfile.PlanningOnly),
                    context.SourceDocumentSha256,
                    () =>
                        context);

        Assert.Equal(
            DocumentDualRunSubmissionStatus.Enqueued,
            result.Status);

        Assert.True(
            result.JobId.HasValue);

        Assert.True(
            dispatcher.TryTake(
                out var job));

        Assert.NotNull(
            job);

        Assert.Equal(
            DocumentDualRunExecutionMode.PlanningOnly,
            job!.Request.ExecutionMode);

        Assert.Equal(
            result.JobId,
            job.JobId);

        Assert.True(
            File.Exists(
                job.SourceSnapshotPath));

        Assert.True(
            File.Exists(
                job.RequestFilePath));

        var jobDirectory =
            job.JobDirectoryPath;

        await job
            .DisposeAsync();

        Assert.False(
            Directory.Exists(
                jobDirectory));
    }

    [Fact]
    public async Task SubmitAsync_Full_EnqueuesFullJob()
    {
        using var scope =
            new TemporaryDirectoryScope();

        await using var dispatcher =
            new DocumentDualRunBoundedJobDispatcher(
                capacity:
                    1);

        var bytes =
            "full"u8
                .ToArray();

        await using var sourceStream =
            new MemoryStream(
                bytes,
                writable:
                    false);

        var context =
            SelectedSubmission(
                sourceStream,
                bytes);

        var coordinator =
            Coordinator(
                scope.Path,
                () =>
                    dispatcher);

        var result =
            await coordinator
                .SubmitAsync(
                    new DocumentDualRunProfileSnapshot(
                        DocumentDualRunProfile.Full),
                    context.SourceDocumentSha256,
                    () =>
                        context);

        Assert.Equal(
            DocumentDualRunSubmissionStatus.Enqueued,
            result.Status);

        Assert.True(
            dispatcher.TryTake(
                out var job));

        Assert.NotNull(
            job);

        Assert.Equal(
            DocumentDualRunExecutionMode.Full,
            job!.Request.ExecutionMode);

        await job
            .DisposeAsync();
    }

    #endregion

    #region Methods Best Effort Failures

    [Fact]
    public async Task SubmitAsync_DispatcherFactoryFailure_IsNonAuthoritativeAndBuildsNoEnvelope()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var selectedFactoryCalls =
            0;

        var coordinator =
            Coordinator(
                scope.Path,
                () =>
                    throw new IOException(
                        "dispatcher unavailable"));

        var sha =
            Sha256(
                "dispatcher-failure"u8.ToArray());

        var result =
            await coordinator
                .SubmitAsync(
                    new DocumentDualRunProfileSnapshot(
                        DocumentDualRunProfile.Full),
                    sha,
                    () =>
                    {
                        selectedFactoryCalls++;

                        throw new InvalidOperationException(
                            "Envelope should not be built when dispatcher resolution fails.");
                    });

        Assert.Equal(
            DocumentDualRunSubmissionStatus.Failed,
            result.Status);

        Assert.Equal(
            DocumentDualRunSubmissionFailureStage.DispatcherResolution,
            result.Failure?.Stage);

        Assert.Equal(
            0,
            selectedFactoryCalls);

        Assert.False(
            Directory.Exists(
                scope.Path));
    }

    [Fact]
    public async Task SubmitAsync_SelectedEnvelopeFailure_IsNonAuthoritativeAndCreatesNoSnapshot()
    {
        using var scope =
            new TemporaryDirectoryScope();

        await using var dispatcher =
            new DocumentDualRunBoundedJobDispatcher(
                capacity:
                    1);

        var coordinator =
            Coordinator(
                scope.Path,
                () =>
                    dispatcher);

        var result =
            await coordinator
                .SubmitAsync(
                    new DocumentDualRunProfileSnapshot(
                        DocumentDualRunProfile.Full),
                    Sha256(
                        "envelope-failure"u8.ToArray()),
                    () =>
                        throw new InvalidOperationException(
                            "synthetic selected-envelope failure"));

        Assert.Equal(
            DocumentDualRunSubmissionStatus.Failed,
            result.Status);

        Assert.Equal(
            DocumentDualRunSubmissionFailureStage.SelectedSubmissionCreation,
            result.Failure?.Stage);

        Assert.False(
            Directory.Exists(
                scope.Path));
    }

    [Fact]
    public async Task SubmitAsync_SnapshotIdentityFailure_IsNonAuthoritativeAndLeavesNoJobDirectory()
    {
        using var scope =
            new TemporaryDirectoryScope();

        await using var dispatcher =
            new DocumentDualRunBoundedJobDispatcher(
                capacity:
                    1);

        var bytes =
            "snapshot-mismatch"u8
                .ToArray();

        await using var sourceStream =
            new MemoryStream(
                bytes,
                writable:
                    false);

        var expectedSha =
            new string(
                '0',
                64);

        var context =
            new DocumentDualRunSelectedSubmission(
                new DocumentSource(
                    sourceStream,
                    "sample.pdf",
                    "application/pdf"),
                expectedSha,
                bytes.Length,
                DocumentFormatId.Pdf,
                "test-engine-v1",
                [
                    Baseline()
                ]);

        var coordinator =
            Coordinator(
                scope.Path,
                () =>
                    dispatcher);

        var result =
            await coordinator
                .SubmitAsync(
                    new DocumentDualRunProfileSnapshot(
                        DocumentDualRunProfile.PlanningOnly),
                    expectedSha,
                    () =>
                        context);

        Assert.Equal(
            DocumentDualRunSubmissionStatus.Failed,
            result.Status);

        Assert.Equal(
            DocumentDualRunSubmissionFailureStage.SourceSnapshot,
            result.Failure?.Stage);

        Assert.Equal(
            0,
            dispatcher.Count);

        AssertNoJobDirectories(
            scope.Path);
    }

    [Fact]
    public async Task SubmitAsync_QueueFull_DropsAndCleansRejectedPreparedJob()
    {
        using var scope =
            new TemporaryDirectoryScope();

        await using var dispatcher =
            new DocumentDualRunBoundedJobDispatcher(
                capacity:
                    1);

        var filler =
            await CreatePreparedJobAsync(
                scope.Path,
                "filler");

        Assert.Equal(
            DocumentDualRunDispatchOutcome.Enqueued,
            dispatcher.TryDispatch(
                filler));

        var bytes =
            "queue-full"u8
                .ToArray();

        await using var sourceStream =
            new MemoryStream(
                bytes,
                writable:
                    false);

        var context =
            SelectedSubmission(
                sourceStream,
                bytes);

        var coordinator =
            Coordinator(
                scope.Path,
                () =>
                    dispatcher);

        var result =
            await coordinator
                .SubmitAsync(
                    new DocumentDualRunProfileSnapshot(
                        DocumentDualRunProfile.Full),
                    context.SourceDocumentSha256,
                    () =>
                        context);

        Assert.Equal(
            DocumentDualRunSubmissionStatus.QueueFull,
            result.Status);

        Assert.Equal(
            1,
            dispatcher.Count);

        Assert.Single(
            Directory.EnumerateDirectories(
                scope.Path));
    }

    [Fact]
    public async Task SubmitAsync_StoppedDispatcher_DropsAndCleansPreparedJob()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var dispatcher =
            new DocumentDualRunBoundedJobDispatcher(
                capacity:
                    1);

        await dispatcher
            .DisposeAsync();

        var bytes =
            "stopped"u8
                .ToArray();

        await using var sourceStream =
            new MemoryStream(
                bytes,
                writable:
                    false);

        var context =
            SelectedSubmission(
                sourceStream,
                bytes);

        var coordinator =
            Coordinator(
                scope.Path,
                () =>
                    dispatcher);

        var result =
            await coordinator
                .SubmitAsync(
                    new DocumentDualRunProfileSnapshot(
                        DocumentDualRunProfile.Full),
                    context.SourceDocumentSha256,
                    () =>
                        context);

        Assert.Equal(
            DocumentDualRunSubmissionStatus.DispatcherStopped,
            result.Status);

        AssertNoJobDirectories(
            scope.Path);
    }

    [Fact]
    public async Task SubmitAsync_DispatchException_IsNonAuthoritativeAndCleansPreparedJob()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var bytes =
            "dispatch-exception"u8
                .ToArray();

        await using var sourceStream =
            new MemoryStream(
                bytes,
                writable:
                    false);

        var context =
            SelectedSubmission(
                sourceStream,
                bytes);

        var coordinator =
            Coordinator(
                scope.Path,
                () =>
                    new ThrowingDispatcher());

        var result =
            await coordinator
                .SubmitAsync(
                    new DocumentDualRunProfileSnapshot(
                        DocumentDualRunProfile.Full),
                    context.SourceDocumentSha256,
                    () =>
                        context);

        Assert.Equal(
            DocumentDualRunSubmissionStatus.Failed,
            result.Status);

        Assert.Equal(
            DocumentDualRunSubmissionFailureStage.Dispatch,
            result.Failure?.Stage);

        AssertNoJobDirectories(
            scope.Path);
    }

    #endregion

    #region Methods Test Data

    private static DocumentDualRunSubmissionCoordinator Coordinator(
        string spoolRoot,
        Func<IDocumentDualRunJobDispatcher> dispatcherFactory) =>
        new(
            new DocumentDualRunSourceSnapshotFactory(
                spoolRoot),
            new DocumentDualRunRequestMaterializer(),
            dispatcherFactory);

    private static DocumentDualRunSelectedSubmission SelectedSubmission(
        Stream sourceStream,
        byte[] sourceBytes) =>
        new(
            new DocumentSource(
                sourceStream,
                "sample.pdf",
                "application/pdf"),
            Sha256(
                sourceBytes),
            sourceBytes.Length,
            DocumentFormatId.Pdf,
            "test-engine-v1",
            [
                Baseline()
            ]);

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

    private static async Task<DocumentDualRunPreparedJob> CreatePreparedJobAsync(
        string spoolRoot,
        string payload)
    {
        var bytes =
            Encoding.UTF8.GetBytes(
                payload);

        await using var source =
            new MemoryStream(
                bytes,
                writable:
                    false);

        var snapshot =
            await new DocumentDualRunSourceSnapshotFactory(
                    spoolRoot)
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
                    Baseline()
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

    private static void AssertNoJobDirectories(
        string spoolRoot)
    {
        Assert.True(
            Directory.Exists(
                spoolRoot));

        Assert.Empty(
            Directory.EnumerateDirectories(
                spoolRoot));
    }

    #endregion

    #region Test Types

    private sealed class ThrowingDispatcher
        : IDocumentDualRunJobDispatcher
    {
        #region Methods Dispatch

        public DocumentDualRunDispatchOutcome TryDispatch(
            DocumentDualRunPreparedJob job)
        {
            ArgumentNullException.ThrowIfNull(
                job);

            throw new InvalidOperationException(
                "synthetic dispatch failure");
        }

        #endregion
    }

    private sealed class TemporaryDirectoryScope
        : IDisposable
    {
        #region ctor

        public TemporaryDirectoryScope()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"dpe-dual-run-submission-test-{Guid.NewGuid():N}");
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
