using DocumentProcessing.Manager.Queue;

namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Outcome of dispatching at most one queued processing unit.
/// </summary>
public sealed record ProcessingDispatchOutcome
{
    #region Properties

    /// <summary>
    /// Gets the terminal dispatch status.
    /// </summary>
    public ProcessingDispatchStatus Status { get; }

    /// <summary>
    /// Gets the claimed processing-unit identity, when one was claimed.
    /// </summary>
    public ProcessingUnitId? UnitId { get; }

    /// <summary>
    /// Gets the retained failure, when dispatch failed or lease renewal broke.
    /// </summary>
    public ProcessingFailure? Failure { get; }

    #endregion

    #region ctor

    internal ProcessingDispatchOutcome(
        ProcessingDispatchStatus status,
        ProcessingUnitId? unitId = null,
        ProcessingFailure? failure = null)
    {
        if (status ==
                ProcessingDispatchStatus.QueueEmpty &&
            unitId is not null)
        {
            throw new ArgumentException(
                "An empty-queue outcome cannot identify a processing unit.",
                nameof(unitId));
        }

        if (status !=
                ProcessingDispatchStatus.QueueEmpty &&
            unitId is null)
        {
            throw new ArgumentException(
                "A claimed dispatch outcome requires a processing-unit identifier.",
                nameof(unitId));
        }

        Status =
            status;

        UnitId =
            unitId;

        Failure =
            failure;
    }

    #endregion
}
