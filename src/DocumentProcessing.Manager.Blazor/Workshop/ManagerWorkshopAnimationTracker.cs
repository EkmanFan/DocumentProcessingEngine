using DocumentProcessing.Manager.Blazor.ManagerApi;

namespace DocumentProcessing.Manager.Blazor.Workshop;

internal sealed class ManagerWorkshopAnimationTracker
{
    #region Variables and Constants

    private readonly HashSet<Guid>
        _observedSuccessfulUnitIds =
            [];

    private bool
        _initialized;

    #endregion

    #region Properties

    public long CelebrationSequence { get; private set; }

    #endregion

    #region Methods

    public void Observe(
        ManagerWorkshopSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        var successfulUnitIds =
            snapshot.CompletedItems
                .Where(
                    item =>
                        item.Status ==
                        ManagerQueueItemStatus.Succeeded)
                .Select(
                    item =>
                        item.UnitId)
                .ToHashSet();

        if (_initialized &&
            successfulUnitIds.Any(
                unitId =>
                    !_observedSuccessfulUnitIds.Contains(
                        unitId)))
        {
            CelebrationSequence++;
        }

        _observedSuccessfulUnitIds.UnionWith(
            successfulUnitIds);

        _initialized =
            true;
    }

    #endregion
}
