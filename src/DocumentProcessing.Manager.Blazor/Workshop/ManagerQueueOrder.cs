namespace DocumentProcessing.Manager.Blazor.Workshop;

internal static class ManagerQueueOrder
{
    #region Methods

    public static IReadOnlyList<Guid> MoveToTargetPosition(
        IReadOnlyList<Guid> orderedUnitIds,
        Guid movingUnitId,
        Guid targetUnitId)
    {
        ArgumentNullException.ThrowIfNull(
            orderedUnitIds);

        if (movingUnitId ==
                Guid.Empty ||
            targetUnitId ==
                Guid.Empty)
        {
            throw new ArgumentException(
                "Queue reorder identifiers cannot be empty.");
        }

        if (orderedUnitIds.Any(
                unitId =>
                    unitId ==
                    Guid.Empty) ||
            orderedUnitIds.Distinct().Count() !=
            orderedUnitIds.Count)
        {
            throw new ArgumentException(
                "Queue order must contain distinct non-empty identifiers.",
                nameof(orderedUnitIds));
        }

        var reordered =
            orderedUnitIds.ToList();

        var movingIndex =
            reordered.IndexOf(
                movingUnitId);

        var targetIndex =
            reordered.IndexOf(
                targetUnitId);

        if (movingIndex <
                0 ||
            targetIndex <
                0)
        {
            throw new ArgumentException(
                "Queue reorder identifiers must belong to the pending order.",
                nameof(orderedUnitIds));
        }

        if (movingIndex ==
            targetIndex)
        {
            return reordered;
        }

        reordered.RemoveAt(
            movingIndex);

        reordered.Insert(
            Math.Min(
                targetIndex,
                reordered.Count),
            movingUnitId);

        return reordered;
    }

    #endregion
}
