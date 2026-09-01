namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Requests atomic replacement of one pending whole-document unit by ordered
/// physical-page units.
/// </summary>
public sealed record SplitPendingProcessingUnitCommand
{
    #region Properties

    public long ExpectedQueueVersion { get; }

    public ProcessingUnitId UnitId { get; }

    public IReadOnlyList<ProcessingUnitIntake> ReplacementUnits { get; }

    #endregion

    #region ctor

    public SplitPendingProcessingUnitCommand(
        long expectedQueueVersion,
        ProcessingUnitId unitId,
        IReadOnlyList<ProcessingUnitIntake> replacementUnits)
    {
        if (expectedQueueVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedQueueVersion));
        }

        if (unitId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Processing-unit identifier cannot be empty.",
                nameof(unitId));
        }

        ArgumentNullException.ThrowIfNull(
            replacementUnits);

        if (replacementUnits.Count == 0 ||
            replacementUnits.Any(
                intake =>
                    intake is null ||
                    intake.WorkItem.Scope is not ProcessingUnitScope.PageRange))
        {
            throw new ArgumentException(
                "At least one page-range replacement unit is required.",
                nameof(replacementUnits));
        }

        ExpectedQueueVersion = expectedQueueVersion;
        UnitId = unitId;
        ReplacementUnits = replacementUnits.ToArray();
    }

    #endregion
}
