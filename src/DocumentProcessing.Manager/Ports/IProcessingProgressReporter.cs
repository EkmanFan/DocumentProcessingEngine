using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Runtime port for publishing the latest progress of an active processing unit.
/// </summary>
public interface IProcessingProgressReporter
{
    /// <summary>Publishes one monotonic progress observation.</summary>
    void Report(
        ProcessingUnitId unitId,
        ProcessingProgressSnapshot progress);
}
