using DocumentProcessing.Manager.Processing;
using DocumentProcessing.Manager.Submissions;

namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Consumer-safe durable snapshot of one processing unit.
/// </summary>
public sealed record ProcessingQueueItemSnapshot
{
    #region Properties

    /// <summary>
    /// Gets the immutable processing work item.
    /// </summary>
    public ProcessingWorkItem WorkItem { get; }

    /// <summary>
    /// Gets the normalized original source filename.
    /// </summary>
    public string OriginalFileName { get; }

    /// <summary>
    /// Gets the durable unit status.
    /// </summary>
    public ProcessingUnitStatus Status { get; }

    /// <summary>
    /// Gets whether this unit is shelved or eligible for dispatch.
    /// </summary>
    public ProcessingUnitDispatchState DispatchState { get; }

    /// <summary>
    /// Gets the global position when the unit is pending.
    /// </summary>
    public long? QueuePosition { get; }

    /// <summary>
    /// Gets the durable result reference after successful completion.
    /// </summary>
    public string? ResultReference { get; }

    /// <summary>
    /// Gets the latest retained technical or terminal failure.
    /// </summary>
    public ProcessingFailure? LastFailure { get; }

    /// <summary>
    /// Gets the latest retained interruption reason.
    /// </summary>
    public ProcessingInterruptionReason? LastInterruptionReason { get; }

    /// <summary>
    /// Gets the durable unit creation instant.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// Gets the latest durable unit update instant.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one durable queue-item snapshot.
    /// </summary>
    public ProcessingQueueItemSnapshot(
        ProcessingWorkItem workItem,
        string originalFileName,
        ProcessingUnitStatus status,
        ProcessingUnitDispatchState dispatchState,
        long? queuePosition,
        string? resultReference,
        ProcessingFailure? lastFailure,
        ProcessingInterruptionReason? lastInterruptionReason,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        WorkItem =
            workItem ??
            throw new ArgumentNullException(
                nameof(workItem));

        OriginalFileName =
            DocumentSubmission.NormalizeFileName(
                originalFileName);

        if (!Enum.IsDefined(
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown processing-unit status.");
        }

        if (!Enum.IsDefined(
                dispatchState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dispatchState),
                dispatchState,
                "Unknown processing-unit dispatch state.");
        }

        if (status !=
                ProcessingUnitStatus.Pending &&
            dispatchState !=
                ProcessingUnitDispatchState.Ready)
        {
            throw new ArgumentException(
                "Only pending processing units may remain shelved.",
                nameof(dispatchState));
        }

        if ((status ==
                ProcessingUnitStatus.Pending) !=
            queuePosition.HasValue)
        {
            throw new ArgumentException(
                "Only pending processing units must retain a queue position.",
                nameof(queuePosition));
        }

        var normalizedResultReference =
            string.IsNullOrWhiteSpace(
                resultReference)
                ? null
                : resultReference.Trim();

        if ((status ==
                ProcessingUnitStatus.Succeeded) !=
            (normalizedResultReference is not null))
        {
            throw new ArgumentException(
                "Only successful processing units must retain a result reference.",
                nameof(resultReference));
        }

        if (status ==
                ProcessingUnitStatus.Failed &&
            lastFailure is null)
        {
            throw new ArgumentException(
                "A terminally failed processing unit must retain its failure.",
                nameof(lastFailure));
        }

        if (lastInterruptionReason is not null &&
            !Enum.IsDefined(
                lastInterruptionReason.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastInterruptionReason),
                lastInterruptionReason,
                "Unknown processing interruption reason.");
        }

        Status =
            status;

        DispatchState =
            dispatchState;

        QueuePosition =
            queuePosition;

        ResultReference =
            normalizedResultReference;

        LastFailure =
            lastFailure;

        LastInterruptionReason =
            lastInterruptionReason;

        CreatedAtUtc =
            createdAtUtc.ToUniversalTime();

        UpdatedAtUtc =
            updatedAtUtc.ToUniversalTime();
    }

    #endregion
}
