namespace DocumentProcessing.Manager.Blazor.ManagerApi;

internal interface IManagerResultClient
{
    ValueTask<ManagerResultContent?> OpenResultAsync(
        string resultReference,
        CancellationToken cancellationToken = default);
}
