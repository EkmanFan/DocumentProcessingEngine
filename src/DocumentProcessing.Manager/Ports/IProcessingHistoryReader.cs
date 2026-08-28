using DocumentProcessing.Manager.History;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Ports;

/// <summary>Outbound read port for recent and archived processing history.</summary>
public interface IProcessingHistoryReader
{
    /// <summary>
    /// Reads pending, active and terminal units updated on or after the cutoff.
    /// </summary>
    ValueTask<ProcessingQueueSnapshot> GetRecentSnapshotAsync(
        DateTimeOffset completedSinceUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Searches terminal units older than the archive boundary.</summary>
    ValueTask<ProcessingArchivePage> SearchArchiveAsync(
        ProcessingArchiveQuery query,
        CancellationToken cancellationToken = default);
}
