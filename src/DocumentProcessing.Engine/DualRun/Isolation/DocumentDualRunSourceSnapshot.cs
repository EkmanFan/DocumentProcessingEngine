namespace DocumentProcessing.Engine.DualRun.Isolation;

/// <summary>
/// Owns one isolated Dual Run job directory and its immutable source.bin
/// snapshot.
///
/// The future worker supervisor owns this object for the complete job lifetime.
/// Disposal is deliberately best-effort because Dual Run cleanup must never
/// invalidate an authoritative result.
/// </summary>
public sealed class DocumentDualRunSourceSnapshot
    : IAsyncDisposable
{
    #region Variables and Constants

    private int _disposed;

    #endregion

    #region Properties

    public Guid JobId { get; }

    public string JobDirectoryPath { get; }

    public string SourceSnapshotPath { get; }

    public string SourceDocumentSha256 { get; }

    public long SourceByteLength { get; }

    #endregion

    #region ctor

    internal DocumentDualRunSourceSnapshot(
        Guid jobId,
        string jobDirectoryPath,
        string sourceSnapshotPath,
        string sourceDocumentSha256,
        long sourceByteLength)
    {
        if (jobId ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Dual Run job ID cannot be empty.",
                nameof(jobId));
        }

        if (string.IsNullOrWhiteSpace(
                jobDirectoryPath))
        {
            throw new ArgumentException(
                "Dual Run job directory cannot be empty.",
                nameof(jobDirectoryPath));
        }

        if (string.IsNullOrWhiteSpace(
                sourceSnapshotPath))
        {
            throw new ArgumentException(
                "Dual Run source snapshot path cannot be empty.",
                nameof(sourceSnapshotPath));
        }

        if (string.IsNullOrWhiteSpace(
                sourceDocumentSha256))
        {
            throw new ArgumentException(
                "Dual Run source SHA-256 cannot be empty.",
                nameof(sourceDocumentSha256));
        }

        if (sourceByteLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceByteLength));
        }

        JobId =
            jobId;

        JobDirectoryPath =
            Path.GetFullPath(
                jobDirectoryPath);

        SourceSnapshotPath =
            Path.GetFullPath(
                sourceSnapshotPath);

        SourceDocumentSha256 =
            sourceDocumentSha256;

        SourceByteLength =
            sourceByteLength;
    }

    #endregion

    #region Methods Lifecycle

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1) !=
            0)
        {
            return ValueTask.CompletedTask;
        }

        DeleteDirectoryBestEffort(
            JobDirectoryPath);

        return ValueTask.CompletedTask;
    }

    private static void DeleteDirectoryBestEffort(
        string path)
    {
        try
        {
            if (Directory.Exists(
                    path))
            {
                Directory.Delete(
                    path,
                    recursive:
                        true);
            }
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
