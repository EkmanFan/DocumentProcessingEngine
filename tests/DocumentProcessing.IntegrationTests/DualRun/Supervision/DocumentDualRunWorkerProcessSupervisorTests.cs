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
using DocumentProcessing.Engine.DualRun.Supervision;

namespace DocumentProcessing.IntegrationTests.DualRun.Supervision;

public sealed class DocumentDualRunWorkerProcessSupervisorTests
{
    #region Variables and Constants

    private const string SelectedSha =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private const string ProjectionSha =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    private const long TestFileBoundary =
        16L *
        1024L *
        1024L;

    #endregion

    #region Methods Worker Bootstrap

    [Fact]
    public async Task RunAsync_RealWorkerBootstrap_ValidatesJobAndReturnsStructuredPlanningFailure()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                "real-worker-bootstrap");

        var jobDirectory =
            job.JobDirectoryPath;

        var supervisor =
            Supervisor(
                WorkerExecutablePath(),
                timeout:
                    TimeSpan.FromSeconds(
                        10));

        var result =
            await supervisor
                .RunAsync(
                    job);

        Assert.Equal(
            DocumentDualRunWorkerProcessOutcome.ResultReceived,
            result.Outcome);

        Assert.Equal(
            0,
            result.ExitCode);

        Assert.NotNull(
            result.WorkerResult);

        Assert.Equal(
            DocumentDualRunWorkerResultStatus.Failed,
            result.WorkerResult!.Status);

        Assert.Equal(
            DocumentDualRunWorkerFailureStage.Planning,
            result.WorkerResult.Failure?.Stage);

        Assert.Equal(
            "PlanningNotImplemented",
            result.WorkerResult.Failure?.ExceptionType);

        Assert.False(
            Directory.Exists(
                jobDirectory));
    }

    [Fact]
    public async Task RunAsync_RealWorkerBootstrap_TamperedSourceReturnsStructuredSourceValidationFailure()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                "tamper-source");

        await File.WriteAllTextAsync(
            job.SourceSnapshotPath,
            "tampered");

        var jobDirectory =
            job.JobDirectoryPath;

        var supervisor =
            Supervisor(
                WorkerExecutablePath(),
                timeout:
                    TimeSpan.FromSeconds(
                        10));

        var result =
            await supervisor
                .RunAsync(
                    job);

        Assert.Equal(
            DocumentDualRunWorkerProcessOutcome.ResultReceived,
            result.Outcome);

        Assert.Equal(
            DocumentDualRunWorkerResultStatus.Failed,
            result.WorkerResult?.Status);

        Assert.Equal(
            DocumentDualRunWorkerFailureStage.SourceValidation,
            result.WorkerResult?.Failure?.Stage);

        Assert.False(
            Directory.Exists(
                jobDirectory));
    }

    #endregion

    #region Methods Process Failures

    [Fact]
    public async Task RunAsync_MissingExecutable_ReturnsLaunchFailedAndCleansJob()
    {
        using var scope =
            new TemporaryDirectoryScope();

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                "missing-executable");

        var jobDirectory =
            job.JobDirectoryPath;

        var missingExecutable =
            Path.Combine(
                scope.Path,
                "does-not-exist-worker");

        var supervisor =
            Supervisor(
                missingExecutable,
                timeout:
                    TimeSpan.FromSeconds(
                        5));

        var result =
            await supervisor
                .RunAsync(
                    job);

        Assert.Equal(
            DocumentDualRunWorkerProcessOutcome.LaunchFailed,
            result.Outcome);

        Assert.False(
            Directory.Exists(
                jobDirectory));
    }

    [Fact]
    public async Task RunAsync_UnixNonZeroProcess_ReturnsNonZeroExitAndCapsStderr()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope =
            new TemporaryDirectoryScope();

        var script =
            await WriteExecutableScriptAsync(
                scope.Path,
                "nonzero.sh",
                """
                #!/usr/bin/env bash
                python3 -c 'import sys; sys.stderr.write("x" * 20000)'
                exit 37
                """);

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                "nonzero");

        var jobDirectory =
            job.JobDirectoryPath;

        var supervisor =
            new DocumentDualRunWorkerProcessSupervisor(
                new DocumentDualRunWorkerProcessConfiguration(
                    script,
                    timeout:
                        TimeSpan.FromSeconds(
                            5),
                    terminationGracePeriod:
                        TimeSpan.FromSeconds(
                            2),
                    maximumRequestFileBytes:
                        TestFileBoundary,
                    maximumResultFileBytes:
                        TestFileBoundary,
                    maximumCapturedStandardErrorCharacters:
                        1024));

        var result =
            await supervisor
                .RunAsync(
                    job);

        Assert.Equal(
            DocumentDualRunWorkerProcessOutcome.NonZeroExit,
            result.Outcome);

        Assert.Equal(
            37,
            result.ExitCode);

        Assert.True(
            result.StandardError.Length <=
            1024);

        Assert.False(
            Directory.Exists(
                jobDirectory));
    }

    [Fact]
    public async Task RunAsync_UnixTimeout_KillsProcessTreeAndCleansJob()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope =
            new TemporaryDirectoryScope();

        var script =
            await WriteExecutableScriptAsync(
                scope.Path,
                "timeout.sh",
                """
                #!/usr/bin/env bash
                sleep 30
                """);

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                "timeout");

        var jobDirectory =
            job.JobDirectoryPath;

        var supervisor =
            Supervisor(
                script,
                timeout:
                    TimeSpan.FromMilliseconds(
                        150));

        var result =
            await supervisor
                .RunAsync(
                    job);

        Assert.Equal(
            DocumentDualRunWorkerProcessOutcome.TimedOut,
            result.Outcome);

        Assert.True(
            result.ProcessTreeKillAttempted);

        Assert.True(
            result.ProcessTerminationConfirmed);

        Assert.False(
            Directory.Exists(
                jobDirectory));
    }

    [Fact]
    public async Task RunAsync_UnixZeroExitWithoutResult_ReturnsMissingResultAndCleansJob()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope =
            new TemporaryDirectoryScope();

        var script =
            await WriteExecutableScriptAsync(
                scope.Path,
                "no-result.sh",
                """
                #!/usr/bin/env bash
                exit 0
                """);

        var job =
            await CreatePreparedJobAsync(
                scope.Path,
                "no-result");

        var jobDirectory =
            job.JobDirectoryPath;

        var supervisor =
            Supervisor(
                script,
                timeout:
                    TimeSpan.FromSeconds(
                        5));

        var result =
            await supervisor
                .RunAsync(
                    job);

        Assert.Equal(
            DocumentDualRunWorkerProcessOutcome.MissingResult,
            result.Outcome);

        Assert.False(
            Directory.Exists(
                jobDirectory));
    }

    #endregion

    #region Methods Test Data

    private static DocumentDualRunWorkerProcessSupervisor Supervisor(
        string executablePath,
        TimeSpan? timeout) =>
        new(
            new DocumentDualRunWorkerProcessConfiguration(
                executablePath,
                timeout,
                terminationGracePeriod:
                    TimeSpan.FromSeconds(
                        2),
                maximumRequestFileBytes:
                    TestFileBoundary,
                maximumResultFileBytes:
                    TestFileBoundary,
                maximumCapturedStandardErrorCharacters:
                    4096));

    private static async Task<DocumentDualRunPreparedJob> CreatePreparedJobAsync(
        string spoolRoot,
        string payload)
    {
        var bytes =
            Encoding.UTF8
                .GetBytes(
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

    private static string WorkerExecutablePath()
    {
        var root =
            FindRepositoryRoot();

        var testOutput =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        var configuration =
            testOutput
                .Parent
                ?.Name ??
            throw new InvalidOperationException(
                "Unable to determine test build configuration.");

        var executableName =
            OperatingSystem.IsWindows()
                ? "DocumentProcessing.DualRunWorker.exe"
                : "DocumentProcessing.DualRunWorker";

        var workerPath =
            Path.Combine(
                root,
                "src",
                "DocumentProcessing.DualRunWorker",
                "bin",
                configuration,
                "net10.0",
                executableName);

        Assert.True(
            File.Exists(
                workerPath),
            $"Expected built worker executable at '{workerPath}'.");

        return workerPath;
    }

    private static string FindRepositoryRoot()
    {
        var current =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        current.FullName,
                        "DocumentProcessingEngine.sln")))
            {
                return current.FullName;
            }

            current =
                current.Parent;
        }

        throw new InvalidOperationException(
            "Unable to locate DocumentProcessingEngine.sln from test output.");
    }

    private static async Task<string> WriteExecutableScriptAsync(
        string directory,
        string fileName,
        string body)
    {
        Directory.CreateDirectory(
            directory);

        var path =
            Path.Combine(
                directory,
                fileName);

        await File.WriteAllTextAsync(
            path,
            body.TrimStart() +
            "\n");

        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Executable test scripts require Unix file permissions.");
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute);

        return path;
    }

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
                    $"dpe-dual-run-supervision-test-{Guid.NewGuid():N}");
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
