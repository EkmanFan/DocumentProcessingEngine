namespace DocumentProcessing.ProviderLifecycle;

internal sealed class ExternalProcessingProviderRuntime
    : IProcessingProviderRuntime
{
    #region Methods

    public ValueTask EnsureAvailableAsync(
        ProcessingProviderCapability capability,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.CompletedTask;
    }

    public void ReportUnavailable(
        ProcessingProviderCapability capability)
    {
    }

    public void Dispose()
    {
    }

    #endregion
}
