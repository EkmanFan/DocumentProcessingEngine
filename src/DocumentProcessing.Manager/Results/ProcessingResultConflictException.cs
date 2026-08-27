using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Results;

/// <summary>
/// Reports reuse of a processing unit for different durable result bytes.
/// </summary>
public sealed class ProcessingResultConflictException
    : InvalidOperationException
{
    #region Properties

    /// <summary>
    /// Gets the processing unit whose result conflicts.
    /// </summary>
    public ProcessingUnitId UnitId { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a processing-result idempotency conflict.
    /// </summary>
    public ProcessingResultConflictException(
        ProcessingUnitId unitId)
        : base(
            $"Processing unit '{unitId}' already owns different durable result metadata.")
    {
        UnitId =
            unitId;
    }

    #endregion
}
