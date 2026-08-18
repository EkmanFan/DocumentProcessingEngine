using System.Globalization;
using System.Security.Cryptography;
using DocumentProcessing.Core.DualRun.Transport;

namespace DocumentProcessing.DualRunWorker;

/// <summary>
/// Process-side V1 bootstrap.
///
/// This checkpoint validates the file-backed job boundary and emits a strict
/// structured Planning failure after validation. Candidate planning/execution is
/// intentionally not wired yet.
/// </summary>
internal static class DocumentDualRunWorkerBootstrap
{
    #region Variables and Constants

    private const int SuccessExitCode =
        0;

    private const int InvalidJobExitCode =
        20;

    private const int ResultWriteFailureExitCode =
        30;

    private const string JobDirectoryArgument =
        "--job-directory";

    private const string MaximumRequestBytesArgument =
        "--max-request-bytes";

    private const string PartialResultFileName =
        "result.json.partial";

    private const int MaximumFailureMessageLength =
        1024;

    private static readonly UnixFileMode PrivateFileMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite;

    #endregion

    #region Methods Entry Point

    public static async Task<int> RunAsync(
        string[] args)
    {
        DocumentDualRunWorkerRequest? request =
            null;

        string? jobDirectoryPath =
            null;

        try
        {
            var invocation =
                ParseInvocation(
                    args);

            jobDirectoryPath =
                invocation.JobDirectoryPath;

            ValidateJobDirectory(
                jobDirectoryPath);

            request =
                await LoadRequestAsync(
                        jobDirectoryPath,
                        invocation.MaximumRequestBytes)
                    .ConfigureAwait(false);

            ValidateRequestPaths(
                jobDirectoryPath,
                request);

            try
            {
                await ValidateSourceAsync(
                        jobDirectoryPath,
                        request)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
                when (IsOrdinaryFailure(
                    exception))
            {
                return await WriteStructuredFailureAsync(
                        jobDirectoryPath,
                        request,
                        DocumentDualRunWorkerFailureStage.SourceValidation,
                        exception)
                    .ConfigureAwait(false);
            }

            var result =
                new DocumentDualRunWorkerResult(
                    request.JobId,
                    request.ExecutionMode,
                    WorkerEngineVersion(),
                    request.SourceDocumentSha256,
                    DocumentDualRunWorkerResultStatus.Failed,
                    [],
                    new DocumentDualRunWorkerFailure(
                        DocumentDualRunWorkerFailureStage.Planning,
                        "PlanningNotImplemented",
                        "Dual Run worker planning execution is not wired at this checkpoint."));

            await WriteResultAsync(
                    jobDirectoryPath,
                    result)
                .ConfigureAwait(false);

            return SuccessExitCode;
        }
        catch (Exception exception)
            when (IsOrdinaryFailure(
                exception))
        {
            if (request is not null &&
                jobDirectoryPath is not null)
            {
                try
                {
                    return await WriteStructuredFailureAsync(
                            jobDirectoryPath,
                            request,
                            DocumentDualRunWorkerFailureStage.Unexpected,
                            exception)
                        .ConfigureAwait(false);
                }
                catch (Exception writeException)
                    when (IsOrdinaryFailure(
                        writeException))
                {
                    WriteSafeStandardError(
                        "Dual Run worker could not materialize result.json.");

                    return ResultWriteFailureExitCode;
                }
            }

            WriteSafeStandardError(
                $"Dual Run worker rejected the job before a trusted request was available: " +
                $"{exception.GetType().Name}.");

            return InvalidJobExitCode;
        }
    }

    #endregion

    #region Methods Invocation

    private static WorkerInvocation ParseInvocation(
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(
            args);

        if (args.Length !=
            4)
        {
            throw new ArgumentException(
                "Dual Run worker requires exactly --job-directory and --max-request-bytes.");
        }

        string? jobDirectory =
            null;

        long? maximumRequestBytes =
            null;

        for (var index = 0;
             index <
             args.Length;
             index +=
                 2)
        {
            var name =
                args[index];

            var value =
                args[index +
                     1];

            switch (name)
            {
                case JobDirectoryArgument:
                    if (jobDirectory is not null)
                    {
                        throw new ArgumentException(
                            "Duplicate --job-directory argument.");
                    }

                    jobDirectory =
                        value;

                    break;

                case MaximumRequestBytesArgument:
                    if (maximumRequestBytes.HasValue)
                    {
                        throw new ArgumentException(
                            "Duplicate --max-request-bytes argument.");
                    }

                    if (!long.TryParse(
                            value,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var parsedMaximumRequestBytes) ||
                        parsedMaximumRequestBytes <=
                        0)
                    {
                        throw new ArgumentException(
                            "--max-request-bytes must be a positive integer.");
                    }

                    maximumRequestBytes =
                        parsedMaximumRequestBytes;

                    break;

                default:
                    throw new ArgumentException(
                        $"Unsupported Dual Run worker argument '{name}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(
                jobDirectory) ||
            !maximumRequestBytes.HasValue)
        {
            throw new ArgumentException(
                "Dual Run worker invocation is incomplete.");
        }

        if (!Path.IsPathFullyQualified(
                jobDirectory))
        {
            throw new ArgumentException(
                "Dual Run worker job directory must be fully qualified.");
        }

        return new WorkerInvocation(
            Path.GetFullPath(
                jobDirectory),
            maximumRequestBytes.Value);
    }

    #endregion

    #region Methods Request

    private static async Task<DocumentDualRunWorkerRequest> LoadRequestAsync(
        string jobDirectoryPath,
        long maximumRequestBytes)
    {
        var requestPath =
            Path.Combine(
                jobDirectoryPath,
                DocumentDualRunTransportSchema
                    .RequestFileName);

        ValidateRegularFile(
            requestPath,
            DocumentDualRunTransportSchema
                .RequestFileName);

        var fileInfo =
            new FileInfo(
                requestPath);

        if (fileInfo.Length <=
                0 ||
            fileInfo.Length >
                maximumRequestBytes)
        {
            throw new InvalidDataException(
                $"Dual Run request.json length {fileInfo.Length} is outside the configured boundary.");
        }

        var bytes =
            await File.ReadAllBytesAsync(
                    requestPath)
                .ConfigureAwait(false);

        return DocumentDualRunTransportJson
            .DeserializeRequest(
                bytes);
    }

    private static void ValidateRequestPaths(
        string jobDirectoryPath,
        DocumentDualRunWorkerRequest request)
    {
        var expectedSourcePath =
            Path.Combine(
                jobDirectoryPath,
                DocumentDualRunTransportSchema
                    .SourceSnapshotFileName);

        if (!PathsEqual(
                request.SourceSnapshotPath,
                expectedSourcePath))
        {
            throw new InvalidDataException(
                "Dual Run request source path does not identify this job directory's source.bin.");
        }

        var expectedRequestPath =
            Path.Combine(
                jobDirectoryPath,
                DocumentDualRunTransportSchema
                    .RequestFileName);

        if (!PathsEqual(
                Path.GetDirectoryName(
                    expectedRequestPath) ??
                string.Empty,
                jobDirectoryPath))
        {
            throw new InvalidDataException(
                "Dual Run request path containment validation failed.");
        }
    }

    #endregion

    #region Methods Source Validation

    private static async Task ValidateSourceAsync(
        string jobDirectoryPath,
        DocumentDualRunWorkerRequest request)
    {
        var sourcePath =
            Path.Combine(
                jobDirectoryPath,
                DocumentDualRunTransportSchema
                    .SourceSnapshotFileName);

        ValidateRegularFile(
            sourcePath,
            DocumentDualRunTransportSchema
                .SourceSnapshotFileName);

        await using var source =
            new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize:
                    81920,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        if (source.Length !=
            request.SourceByteLength)
        {
            throw new InvalidDataException(
                $"Dual Run source byte length {source.Length} does not match request length " +
                $"{request.SourceByteLength}.");
        }

        using var sha256 =
            SHA256.Create();

        var observedSha256 =
            Convert
                .ToHexString(
                    await sha256
                        .ComputeHashAsync(
                            source)
                        .ConfigureAwait(false))
                .ToLowerInvariant();

        if (!string.Equals(
                observedSha256,
                request.SourceDocumentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Dual Run source SHA-256 does not match request identity.");
        }
    }

    #endregion

    #region Methods Result

    private static async Task<int> WriteStructuredFailureAsync(
        string jobDirectoryPath,
        DocumentDualRunWorkerRequest request,
        DocumentDualRunWorkerFailureStage stage,
        Exception exception)
    {
        var result =
            new DocumentDualRunWorkerResult(
                request.JobId,
                request.ExecutionMode,
                WorkerEngineVersion(),
                request.SourceDocumentSha256,
                DocumentDualRunWorkerResultStatus.Failed,
                [],
                new DocumentDualRunWorkerFailure(
                    stage,
                    exception.GetType().FullName ??
                    exception.GetType().Name,
                    SanitizeFailureMessage(
                        exception.Message)));

        await WriteResultAsync(
                jobDirectoryPath,
                result)
            .ConfigureAwait(false);

        return SuccessExitCode;
    }

    private static async Task WriteResultAsync(
        string jobDirectoryPath,
        DocumentDualRunWorkerResult result)
    {
        var resultPath =
            Path.Combine(
                jobDirectoryPath,
                DocumentDualRunTransportSchema
                    .ResultFileName);

        var partialResultPath =
            Path.Combine(
                jobDirectoryPath,
                PartialResultFileName);

        if (File.Exists(
                resultPath) ||
            File.Exists(
                partialResultPath))
        {
            throw new IOException(
                "Dual Run worker result materialization requires an unused result path.");
        }

        var bytes =
            DocumentDualRunTransportJson
                .SerializeResultToUtf8Bytes(
                    result);

        var options =
            new FileStreamOptions
            {
                Mode =
                    FileMode.CreateNew,
                Access =
                    FileAccess.Write,
                Share =
                    FileShare.None,
                BufferSize =
                    4096,
                Options =
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan
            };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode =
                PrivateFileMode;
        }

        var promoted =
            false;

        try
        {
            await using (
                var destination =
                    new FileStream(
                        partialResultPath,
                        options))
            {
                await destination
                    .WriteAsync(
                        bytes)
                    .ConfigureAwait(false);

                await destination
                    .FlushAsync()
                    .ConfigureAwait(false);
            }

            File.Move(
                partialResultPath,
                resultPath);

            promoted =
                true;
        }
        finally
        {
            if (!promoted)
            {
                DeleteFileBestEffort(
                    partialResultPath);
            }
        }
    }

    #endregion

    #region Methods File-System Validation

    private static void ValidateJobDirectory(
        string jobDirectoryPath)
    {
        var directory =
            new DirectoryInfo(
                jobDirectoryPath);

        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException(
                "Dual Run worker job directory does not exist.");
        }

        directory.Refresh();

        if (directory.LinkTarget is not null)
        {
            throw new InvalidDataException(
                "Dual Run worker job directory cannot be a symbolic link.");
        }
    }

    private static void ValidateRegularFile(
        string path,
        string description)
    {
        var file =
            new FileInfo(
                path);

        if (!file.Exists)
        {
            throw new FileNotFoundException(
                $"Dual Run worker required file '{description}' does not exist.");
        }

        file.Refresh();

        if (file.LinkTarget is not null)
        {
            throw new InvalidDataException(
                $"Dual Run worker required file '{description}' cannot be a symbolic link.");
        }
    }

    private static bool PathsEqual(
        string first,
        string second) =>
        string.Equals(
            Path.GetFullPath(
                first),
            Path.GetFullPath(
                second),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    #endregion

    #region Methods Failure Hygiene

    private static string WorkerEngineVersion() =>
        typeof(DocumentDualRunWorkerBootstrap)
            .Assembly
            .GetName()
            .Version
            ?.ToString() ??
        "0.0.0.0";

    private static string SanitizeFailureMessage(
        string? value)
    {
        var normalized =
            string.IsNullOrWhiteSpace(
                value)
                ? "<no message>"
                : value
                    .Replace(
                        '\r',
                        ' ')
                    .Replace(
                        '\n',
                        ' ')
                    .Trim();

        return normalized.Length <=
            MaximumFailureMessageLength
            ? normalized
            : normalized[
                ..MaximumFailureMessageLength];
    }

    private static void WriteSafeStandardError(
        string message)
    {
        try
        {
            Console.Error.WriteLine(
                message);
        }
        catch
        {
        }
    }

    private static bool IsOrdinaryFailure(
        Exception exception) =>
        exception is not OutOfMemoryException;

    private static void DeleteFileBestEffort(
        string path)
    {
        try
        {
            File.Delete(
                path);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    #endregion

    #region Internal Types

    private readonly record struct WorkerInvocation(
        string JobDirectoryPath,
        long MaximumRequestBytes);

    #endregion
}
