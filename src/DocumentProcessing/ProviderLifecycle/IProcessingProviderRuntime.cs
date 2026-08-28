namespace DocumentProcessing.ProviderLifecycle;

internal interface IProcessingProviderRuntime
    : IDisposable
{
    ValueTask EnsureAvailableAsync(
        ProcessingProviderCapability capability,
        CancellationToken cancellationToken);

    void ReportUnavailable(
        ProcessingProviderCapability capability);
}
