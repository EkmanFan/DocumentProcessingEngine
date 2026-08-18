using System.Diagnostics;
using System.Globalization;
using System.Text;
using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Engine.DualRun.Dispatch;

namespace DocumentProcessing.Engine.DualRun.Supervision;

/// <summary>
/// Owns one prepared job for the complete child-process lifetime.
///
/// No shell is used. The worker executable receives only a trusted job-directory
/// argument and an explicit request-size boundary.
///
/// This class supervises one process. It does not create a background queue
/// consumer and does not impose OS cgroup/container resource controls.
/// </summary>
public sealed class DocumentDualRunWorkerProcessSupervisor
{
    #region Variables and Constants

    private const string PartialResultFileName =
        "result.json.partial";

    private readonly DocumentDualRunWorkerProcessConfiguration _configuration;

    #endregion

    #region ctor

    public DocumentDualRunWorkerProcessSupervisor(
        DocumentDualRunWorkerProcessConfiguration configuration)
    {
        _configuration =
            configuration ??
            throw new ArgumentNullException(
                nameof(configuration));
    }

    #endregion

    #region Methods Supervision

    public async ValueTask<DocumentDualRunWorkerProcessRunResult> RunAsync(
        DocumentDualRunPreparedJob job,
        CancellationToken supervisorCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            job);

        await using var ownedJob =
            job;

        var resultPath =
            Path.Combine(
                job.JobDirectoryPath,
                DocumentDualRunTransportSchema
                    .ResultFileName);

        var partialResultPath =
            Path.Combine(
                job.JobDirectoryPath,
                PartialResultFileName);

        try
        {
            ValidatePreparedJobBeforeLaunch(
                job,
                resultPath,
                partialResultPath);
        }
        catch (Exception exception)
            when (IsOrdinaryFailure(
                exception))
        {
            return DocumentDualRunWorkerProcessRunResult
                .WithoutResult(
                    DocumentDualRunWorkerProcessOutcome.InvalidJob,
                    exitCode:
                        null,
                    standardError:
                        string.Empty,
                    exception);
        }

        using var process =
            new Process
            {
                StartInfo =
                    CreateStartInfo(
                        job)
            };

        try
        {
            if (!process.Start())
            {
                return DocumentDualRunWorkerProcessRunResult
                    .WithoutResult(
                        DocumentDualRunWorkerProcessOutcome.LaunchFailed,
                        exitCode:
                            null,
                        standardError:
                            string.Empty,
                        new InvalidOperationException(
                            "Dual Run worker process did not start."));
            }
        }
        catch (Exception exception)
            when (IsOrdinaryFailure(
                exception))
        {
            return DocumentDualRunWorkerProcessRunResult
                .WithoutResult(
                    DocumentDualRunWorkerProcessOutcome.LaunchFailed,
                    exitCode:
                        null,
                    standardError:
                        string.Empty,
                    exception);
        }

        using var outputCancellation =
            new CancellationTokenSource();

        var stdoutDrain =
            DrainAsync(
                process.StandardOutput,
                captureLimit:
                    0,
                outputCancellation.Token);

        var stderrDrain =
            DrainAsync(
                process.StandardError,
                _configuration
                    .MaximumCapturedStandardErrorCharacters,
                outputCancellation.Token);

        using var timeoutCancellation =
            _configuration.Timeout.HasValue
                ? new CancellationTokenSource(
                    _configuration.Timeout.Value)
                : null;

        using var waitCancellation =
            timeoutCancellation is null
                ? CancellationTokenSource.CreateLinkedTokenSource(
                    supervisorCancellationToken)
                : CancellationTokenSource.CreateLinkedTokenSource(
                    supervisorCancellationToken,
                    timeoutCancellation.Token);

        try
        {
            await process
                .WaitForExitAsync(
                    waitCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (supervisorCancellationToken.IsCancellationRequested)
        {
            var termination =
                await TerminateProcessTreeAsync(
                        process)
                    .ConfigureAwait(false);

            outputCancellation.Cancel();

            var standardError =
                await CompleteDrainAsync(
                        stderrDrain)
                    .ConfigureAwait(false);

            await CompleteDrainAsync(
                    stdoutDrain)
                .ConfigureAwait(false);

            return DocumentDualRunWorkerProcessRunResult
                .WithoutResult(
                    DocumentDualRunWorkerProcessOutcome.Cancelled,
                    SafeExitCode(
                        process),
                    standardError,
                    processTreeKillAttempted:
                        termination.KillAttempted,
                    processTerminationConfirmed:
                        termination.TerminationConfirmed);
        }
        catch (OperationCanceledException)
            when (timeoutCancellation?.IsCancellationRequested ==
                  true)
        {
            var termination =
                await TerminateProcessTreeAsync(
                        process)
                    .ConfigureAwait(false);

            outputCancellation.Cancel();

            var standardError =
                await CompleteDrainAsync(
                        stderrDrain)
                    .ConfigureAwait(false);

            await CompleteDrainAsync(
                    stdoutDrain)
                .ConfigureAwait(false);

            return DocumentDualRunWorkerProcessRunResult
                .WithoutResult(
                    DocumentDualRunWorkerProcessOutcome.TimedOut,
                    SafeExitCode(
                        process),
                    standardError,
                    processTreeKillAttempted:
                        termination.KillAttempted,
                    processTerminationConfirmed:
                        termination.TerminationConfirmed);
        }
        catch (Exception exception)
            when (IsOrdinaryFailure(
                exception))
        {
            var termination =
                await TerminateProcessTreeAsync(
                        process)
                    .ConfigureAwait(false);

            outputCancellation.Cancel();

            var standardError =
                await CompleteDrainAsync(
                        stderrDrain)
                    .ConfigureAwait(false);

            await CompleteDrainAsync(
                    stdoutDrain)
                .ConfigureAwait(false);

            return DocumentDualRunWorkerProcessRunResult
                .WithoutResult(
                    DocumentDualRunWorkerProcessOutcome.SupervisionFailed,
                    SafeExitCode(
                        process),
                    standardError,
                    exception,
                    termination.KillAttempted,
                    termination.TerminationConfirmed);
        }

        var finalStandardError =
            await CompleteDrainAsync(
                    stderrDrain)
                .ConfigureAwait(false);

        await CompleteDrainAsync(
                stdoutDrain)
            .ConfigureAwait(false);

        var exitCode =
            process.ExitCode;

        if (exitCode !=
            0)
        {
            return DocumentDualRunWorkerProcessRunResult
                .WithoutResult(
                    DocumentDualRunWorkerProcessOutcome.NonZeroExit,
                    exitCode,
                    finalStandardError,
                    processTreeKillAttempted:
                        false,
                    processTerminationConfirmed:
                        true);
        }

        try
        {
            var workerResult =
                await ReadAndValidateResultAsync(
                        job,
                        resultPath,
                        partialResultPath)
                    .ConfigureAwait(false);

            return DocumentDualRunWorkerProcessRunResult
                .ResultReceived(
                    exitCode,
                    workerResult,
                    finalStandardError);
        }
        catch (FileNotFoundException exception)
        {
            return DocumentDualRunWorkerProcessRunResult
                .WithoutResult(
                    DocumentDualRunWorkerProcessOutcome.MissingResult,
                    exitCode,
                    finalStandardError,
                    exception,
                    processTerminationConfirmed:
                        true);
        }
        catch (Exception exception)
            when (IsOrdinaryFailure(
                exception))
        {
            return DocumentDualRunWorkerProcessRunResult
                .WithoutResult(
                    DocumentDualRunWorkerProcessOutcome.InvalidResult,
                    exitCode,
                    finalStandardError,
                    exception,
                    processTerminationConfirmed:
                        true);
        }
    }

    #endregion

    #region Methods Launch

    private ProcessStartInfo CreateStartInfo(
        DocumentDualRunPreparedJob job)
    {
        var startInfo =
            new ProcessStartInfo(
                _configuration
                    .WorkerExecutablePath)
            {
                UseShellExecute =
                    false,
                CreateNoWindow =
                    true,
                RedirectStandardOutput =
                    true,
                RedirectStandardError =
                    true,
                WorkingDirectory =
                    job.JobDirectoryPath,
                StandardOutputEncoding =
                    Encoding.UTF8,
                StandardErrorEncoding =
                    Encoding.UTF8
            };

        startInfo.ArgumentList.Add(
            "--job-directory");

        startInfo.ArgumentList.Add(
            job.JobDirectoryPath);

        startInfo.ArgumentList.Add(
            "--max-request-bytes");

        startInfo.ArgumentList.Add(
            _configuration
                .MaximumRequestFileBytes
                .ToString(
                    CultureInfo.InvariantCulture));

        return startInfo;
    }

    #endregion

    #region Methods Prepared Job Validation

    private void ValidatePreparedJobBeforeLaunch(
        DocumentDualRunPreparedJob job,
        string resultPath,
        string partialResultPath)
    {
        if (!Directory.Exists(
                job.JobDirectoryPath))
        {
            throw new DirectoryNotFoundException(
                "Dual Run prepared job directory does not exist.");
        }

        if (!File.Exists(
                job.SourceSnapshotPath))
        {
            throw new FileNotFoundException(
                "Dual Run prepared job source.bin does not exist.");
        }

        if (!File.Exists(
                job.RequestFilePath))
        {
            throw new FileNotFoundException(
                "Dual Run prepared job request.json does not exist.");
        }

        var requestLength =
            new FileInfo(
                job.RequestFilePath)
                .Length;

        if (requestLength <=
                0 ||
            requestLength >
                _configuration.MaximumRequestFileBytes)
        {
            throw new InvalidDataException(
                $"Dual Run request.json length {requestLength} is outside the configured boundary.");
        }

        if (File.Exists(
                resultPath) ||
            File.Exists(
                partialResultPath))
        {
            throw new InvalidDataException(
                "Dual Run prepared job contains a stale result path before process launch.");
        }
    }

    #endregion

    #region Methods Result Validation

    private async Task<DocumentDualRunWorkerResult> ReadAndValidateResultAsync(
        DocumentDualRunPreparedJob job,
        string resultPath,
        string partialResultPath)
    {
        if (!File.Exists(
                resultPath))
        {
            throw new FileNotFoundException(
                "Dual Run worker exited successfully without result.json.");
        }

        if (File.Exists(
                partialResultPath))
        {
            throw new InvalidDataException(
                "Dual Run worker left result.json.partial beside a promoted result.");
        }

        var resultFile =
            new FileInfo(
                resultPath);

        resultFile.Refresh();

        if (resultFile.LinkTarget is not null)
        {
            throw new InvalidDataException(
                "Dual Run worker result.json cannot be a symbolic link.");
        }

        if (resultFile.Length <=
                0 ||
            resultFile.Length >
                _configuration.MaximumResultFileBytes)
        {
            throw new InvalidDataException(
                $"Dual Run result.json length {resultFile.Length} is outside the configured boundary.");
        }

        var bytes =
            await File.ReadAllBytesAsync(
                    resultPath)
                .ConfigureAwait(false);

        var result =
            DocumentDualRunTransportJson
                .DeserializeResult(
                    bytes);

        if (result.JobId !=
            job.Request.JobId)
        {
            throw new InvalidDataException(
                "Dual Run worker result job ID does not match the request.");
        }

        if (result.ExecutionMode !=
            job.Request.ExecutionMode)
        {
            throw new InvalidDataException(
                "Dual Run worker result execution mode does not match the request.");
        }

        if (!string.Equals(
                result.SourceDocumentSha256,
                job.Request.SourceDocumentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Dual Run worker result source SHA-256 does not match the request.");
        }

        return result;
    }

    #endregion

    #region Methods Process Termination

    private async ValueTask<ProcessTerminationEvidence> TerminateProcessTreeAsync(
        Process process)
    {
        var killAttempted =
            false;

        try
        {
            if (process.HasExited)
            {
                return new ProcessTerminationEvidence(
                    killAttempted,
                    TerminationConfirmed:
                        true);
            }

            killAttempted =
                true;

            process.Kill(
                entireProcessTree:
                    true);
        }
        catch (Exception exception)
            when (IsOrdinaryFailure(
                exception))
        {
            return new ProcessTerminationEvidence(
                killAttempted,
                SafeHasExited(
                    process));
        }

        using var graceCancellation =
            new CancellationTokenSource(
                _configuration
                    .TerminationGracePeriod);

        try
        {
            await process
                .WaitForExitAsync(
                    graceCancellation.Token)
                .ConfigureAwait(false);

            return new ProcessTerminationEvidence(
                killAttempted,
                TerminationConfirmed:
                    true);
        }
        catch (Exception exception)
            when (exception is OperationCanceledException ||
                  IsOrdinaryFailure(
                      exception))
        {
            return new ProcessTerminationEvidence(
                killAttempted,
                SafeHasExited(
                    process));
        }
    }

    private static int? SafeExitCode(
        Process process)
    {
        try
        {
            return process.HasExited
                ? process.ExitCode
                : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool SafeHasExited(
        Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    #endregion

    #region Methods Output Drain

    private static async Task<string> DrainAsync(
        StreamReader reader,
        int captureLimit,
        CancellationToken cancellationToken)
    {
        var captured =
            captureLimit >
            0
                ? new StringBuilder(
                    Math.Min(
                        captureLimit,
                        4096))
                : null;

        var buffer =
            new char[4096];

        try
        {
            while (true)
            {
                var read =
                    await reader
                        .ReadAsync(
                            buffer.AsMemory(),
                            cancellationToken)
                        .ConfigureAwait(false);

                if (read ==
                    0)
                {
                    break;
                }

                if (captured is null ||
                    captured.Length >=
                    captureLimit)
                {
                    continue;
                }

                var remaining =
                    captureLimit -
                    captured.Length;

                captured.Append(
                    buffer,
                    0,
                    Math.Min(
                        read,
                        remaining));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        return captured?.ToString() ??
               string.Empty;
    }

    private static async Task<string> CompleteDrainAsync(
        Task<string> drainTask)
    {
        try
        {
            return await drainTask
                .ConfigureAwait(false);
        }
        catch (Exception exception)
            when (IsOrdinaryFailure(
                exception))
        {
            return string.Empty;
        }
    }

    #endregion

    #region Methods Failure Boundary

    private static bool IsOrdinaryFailure(
        Exception exception) =>
        exception is not OutOfMemoryException;

    #endregion

    #region Internal Types

    private readonly record struct ProcessTerminationEvidence(
        bool KillAttempted,
        bool TerminationConfirmed);

    #endregion
}
