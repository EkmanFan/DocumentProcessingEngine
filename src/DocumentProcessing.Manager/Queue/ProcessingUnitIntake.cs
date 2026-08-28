namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Defines one immutable work item and its initial dispatch eligibility.
/// </summary>
public sealed record ProcessingUnitIntake
{
    #region Properties

    /// <summary>
    /// Gets the immutable processing work item.
    /// </summary>
    public ProcessingWorkItem WorkItem { get; }

    /// <summary>
    /// Gets the initial dispatch state.
    /// </summary>
    public ProcessingUnitDispatchState DispatchState { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one processing-unit intake.
    /// </summary>
    public ProcessingUnitIntake(
        ProcessingWorkItem workItem,
        ProcessingUnitDispatchState dispatchState)
    {
        WorkItem =
            workItem ??
            throw new ArgumentNullException(
                nameof(workItem));

        if (!Enum.IsDefined(
                dispatchState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dispatchState),
                dispatchState,
                "Unknown processing-unit dispatch state.");
        }

        DispatchState =
            dispatchState;
    }

    #endregion
}
