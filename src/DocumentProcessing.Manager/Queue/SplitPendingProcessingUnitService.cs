using DocumentProcessing.Manager.Ports;

namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Replaces one pending whole-document unit by an ordered, validated native
/// range plan.
/// </summary>
public sealed class SplitPendingProcessingUnitService
{
    #region Variables and Constants

    private readonly IProcessingQueueReader _queueReader;
    private readonly IProcessingQueueStore _queueStore;

    #endregion

    #region ctor

    /// <summary>Creates the pending-unit split use case.</summary>
    public SplitPendingProcessingUnitService(
        IProcessingQueueReader queueReader,
        IProcessingQueueStore queueStore)
    {
        _queueReader = queueReader ?? throw new ArgumentNullException(nameof(queueReader));
        _queueStore = queueStore ?? throw new ArgumentNullException(nameof(queueStore));
    }

    #endregion

    #region Methods

    /// <summary>Applies a split plan against one expected queue version.</summary>
    public async ValueTask<IReadOnlyList<ProcessingUnitId>> SplitAsync(
        ProcessingUnitId unitId,
        long expectedQueueVersion,
        IReadOnlyList<ProcessingUnitScope> ranges,
        ProcessingUnitDispatchState? replacementDispatchState = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ranges);

        ValidateRanges(ranges);

        var snapshot =
            await _queueReader.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        if (snapshot.Version != expectedQueueVersion)
        {
            throw new ProcessingQueueConcurrencyException(expectedQueueVersion, snapshot.Version);
        }

        var sourceUnit =
            snapshot.Items.SingleOrDefault(item => item.WorkItem.UnitId == unitId) ??
            throw new InvalidOperationException("The processing unit no longer exists.");

        if (sourceUnit.Status != ProcessingUnitStatus.Pending ||
            sourceUnit.WorkItem.Scope is not ProcessingUnitScope.WholeDocument)
        {
            throw new InvalidOperationException(
                "Only a pending whole-document unit can be split.");
        }

        var replacements =
            ranges.Select(
                    range =>
                        new ProcessingUnitIntake(
                            new ProcessingWorkItem(
                                ProcessingUnitId.New(),
                                sourceUnit.WorkItem.SubmissionId,
                                range,
                                attemptNumber: 1),
                            replacementDispatchState ?? sourceUnit.DispatchState))
                .ToArray();

        await _queueStore.SplitPendingAsync(
                new SplitPendingProcessingUnitCommand(
                    expectedQueueVersion,
                    unitId,
                    replacements),
                cancellationToken)
            .ConfigureAwait(false);

        return replacements.Select(intake => intake.WorkItem.UnitId).ToArray();
    }

    private static void ValidateRanges(
        IReadOnlyList<ProcessingUnitScope> ranges)
    {
        if (ranges.Count == 0)
        {
            throw new ArgumentException(
                "At least one range is required.",
                nameof(ranges));
        }

        if (ranges.All(
                range =>
                    range is ProcessingUnitScope.PageRange))
        {
            ValidatePageRanges(
                ranges.Cast<ProcessingUnitScope.PageRange>());

            return;
        }

        if (ranges.All(
                range =>
                    range is ProcessingUnitScope.ContentUnitRange))
        {
            ValidateContentUnitRanges(
                ranges.Cast<ProcessingUnitScope.ContentUnitRange>());

            return;
        }

        throw new ArgumentException(
            "Ranges must use one supported native coordinate system.",
            nameof(ranges));
    }

    private static void ValidatePageRanges(
        IEnumerable<ProcessingUnitScope.PageRange> ranges)
    {
        ProcessingUnitScope.PageRange? previous =
            null;

        foreach (var range in
                 ranges)
        {
            if (previous is not null &&
                range.StartPhysicalPageNumber <=
                previous.EndPhysicalPageNumber)
            {
                throw new ArgumentException(
                    "Page ranges must be ordered and cannot overlap.",
                    nameof(ranges));
            }

            previous =
                range;
        }
    }

    private static void ValidateContentUnitRanges(
        IEnumerable<ProcessingUnitScope.ContentUnitRange> ranges)
    {
        ProcessingUnitScope.ContentUnitRange? previous =
            null;

        foreach (var range in
                 ranges)
        {
            if (previous is not null &&
                range.StartContentUnitIndex <=
                previous.EndContentUnitIndex)
            {
                throw new ArgumentException(
                    "Content-unit ranges must be ordered and cannot overlap.",
                    nameof(ranges));
            }

            previous =
                range;
        }
    }

    #endregion
}
