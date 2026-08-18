using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Engine.DualRun.Isolation;

namespace DocumentProcessing.Engine.DualRun.Dispatch;

/// <summary>
/// Materializes the strict V1 request contract into the already-private Dual Run
/// job directory.
///
/// On success, ownership of the supplied source snapshot transfers to the
/// returned DocumentDualRunPreparedJob. On failure, ownership remains with the
/// caller.
/// </summary>
public sealed class DocumentDualRunRequestMaterializer
{
    #region Variables and Constants

    private const string PartialRequestFileName =
        "request.json.partial";

    private static readonly UnixFileMode PrivateFileMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite;

    #endregion

    #region Methods Materialization

    public async ValueTask<DocumentDualRunPreparedJob> CreateAsync(
        DocumentDualRunSourceSnapshot sourceSnapshot,
        DocumentDualRunWorkerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            sourceSnapshot);

        ArgumentNullException.ThrowIfNull(
            request);

        ValidateRequestMatchesSnapshot(
            sourceSnapshot,
            request);

        cancellationToken
            .ThrowIfCancellationRequested();

        var requestFilePath =
            Path.Combine(
                sourceSnapshot.JobDirectoryPath,
                DocumentDualRunTransportSchema
                    .RequestFileName);

        var partialRequestFilePath =
            Path.Combine(
                sourceSnapshot.JobDirectoryPath,
                PartialRequestFileName);

        if (File.Exists(
                requestFilePath) ||
            File.Exists(
                partialRequestFilePath))
        {
            throw new IOException(
                "Dual Run request materialization requires an unused job directory.");
        }

        var json =
            DocumentDualRunTransportJson
                .SerializeRequestToUtf8Bytes(
                    request);

        var promoted =
            false;

        try
        {
            await WritePrivateFileAsync(
                    partialRequestFilePath,
                    json,
                    cancellationToken)
                .ConfigureAwait(false);

            cancellationToken
                .ThrowIfCancellationRequested();

            File.Move(
                partialRequestFilePath,
                requestFilePath);

            promoted =
                true;

            return new DocumentDualRunPreparedJob(
                sourceSnapshot,
                request,
                requestFilePath);
        }
        finally
        {
            if (!promoted)
            {
                DeleteFileBestEffort(
                    partialRequestFilePath);
            }
        }
    }

    #endregion

    #region Methods Validation

    private static void ValidateRequestMatchesSnapshot(
        DocumentDualRunSourceSnapshot sourceSnapshot,
        DocumentDualRunWorkerRequest request)
    {
        if (request.JobId !=
            sourceSnapshot.JobId)
        {
            throw new ArgumentException(
                "Dual Run request job ID must match the source snapshot.",
                nameof(request));
        }

        if (!PathsEqual(
                request.SourceSnapshotPath,
                sourceSnapshot.SourceSnapshotPath))
        {
            throw new ArgumentException(
                "Dual Run request source path must reference the owned source snapshot.",
                nameof(request));
        }

        if (!string.Equals(
                request.SourceDocumentSha256,
                sourceSnapshot.SourceDocumentSha256,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Dual Run request SHA-256 must match the source snapshot.",
                nameof(request));
        }

        if (request.SourceByteLength !=
            sourceSnapshot.SourceByteLength)
        {
            throw new ArgumentException(
                "Dual Run request byte length must match the source snapshot.",
                nameof(request));
        }

        var sourceDirectory =
            Path.GetDirectoryName(
                sourceSnapshot.SourceSnapshotPath);

        if (sourceDirectory is null ||
            !PathsEqual(
                sourceDirectory,
                sourceSnapshot.JobDirectoryPath))
        {
            throw new InvalidDataException(
                "Dual Run source snapshot must reside directly in its owned job directory.");
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

    #region Methods File I/O

    private static async ValueTask WritePrivateFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
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

        await using var destination =
            new FileStream(
                path,
                options);

        await destination
            .WriteAsync(
                content,
                cancellationToken)
            .ConfigureAwait(false);

        await destination
            .FlushAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

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
}
