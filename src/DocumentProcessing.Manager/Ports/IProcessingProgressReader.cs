using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Ports;

/// <summary>
/// Runtime port for reading the latest progress of an active processing unit.
/// </summary>
public interface IProcessingProgressReader
{
    /// <summary>Returns the latest observation for the requested unit, when available.</summary>
    ProcessingProgressSnapshot? TryGet(
        ProcessingUnitId unitId);
}
