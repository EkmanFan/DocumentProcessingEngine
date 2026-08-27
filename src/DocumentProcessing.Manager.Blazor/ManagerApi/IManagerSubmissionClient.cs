namespace DocumentProcessing.Manager.Blazor.ManagerApi;

internal interface IManagerSubmissionClient
{
    ValueTask<ManagerDocumentSubmissionResult> SubmitDocumentAsync(
        ManagerDocumentSubmissionRequest request,
        CancellationToken cancellationToken = default);
}
