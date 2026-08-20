using DocumentProcessing.Core.DualRun.Transport;
using DocumentProcessing.Engine.DualRun.Isolation;

namespace DocumentProcessing.Engine.DualRun.Dispatch;

/// <summary>
/// Owns one fully materialized local Dual Run job:
/// private source.bin + validated request.json.
///
/// Ownership is transferred explicitly:
/// materializer -> caller -> dispatcher queue -> future consumer/supervisor.
/// Disposal recursively releases the underlying private job directory.
/// </summary>
public sealed class DocumentDualRunPreparedJob
    : IAsyncDisposable
{
    #region Variables and Constants

    private readonly DocumentDualRunSourceSnapshot _sourceSnapshot;

    private int _disposed;

    #endregion

    #region Properties

    public Guid JobId =>
        Request.JobId;

    public string JobDirectoryPath =>
        _sourceSnapshot.JobDirectoryPath;

    public string SourceSnapshotPath =>
        _sourceSnapshot.SourceSnapshotPath;

    public string RequestFilePath { get; }

    public DocumentDualRunWorkerRequest Request { get; }

    #endregion

    #region ctor

    internal DocumentDualRunPreparedJob(
        DocumentDualRunSourceSnapshot sourceSnapshot,
        DocumentDualRunWorkerRequest request,
        string requestFilePath)
    {
        ArgumentNullException.ThrowIfNull(
            sourceSnapshot);

        ArgumentNullException.ThrowIfNull(
            request);

        if (string.IsNullOrWhiteSpace(
                requestFilePath))
        {
            throw new ArgumentException(
                "Dual Run request file path cannot be empty.",
                nameof(requestFilePath));
        }

        _sourceSnapshot =
            sourceSnapshot;

        Request =
            request;

        RequestFilePath =
            Path.GetFullPath(
                requestFilePath);
    }

    #endregion

    #region Methods Lifecycle

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposed,
                1) !=
            0)
        {
            return;
        }

        await _sourceSnapshot
            .DisposeAsync()
            .ConfigureAwait(false);
    }

    #endregion
}
