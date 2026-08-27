using DocumentProcessing.Manager.Blazor.Workshop;

namespace DocumentProcessing.Manager.Blazor.ManagerApi;

internal interface IManagerHostClient
{
    ValueTask<ManagerWorkshopSnapshot> GetWorkshopAsync(
        CancellationToken cancellationToken = default);

    ValueTask ExecuteControlAsync(
        ManagerControlAction action,
        CancellationToken cancellationToken = default);
}
