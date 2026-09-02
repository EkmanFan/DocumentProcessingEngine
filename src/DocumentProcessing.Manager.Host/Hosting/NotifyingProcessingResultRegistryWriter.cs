using DocumentProcessing.Manager.Ports;
using DocumentProcessing.Manager.Results;

namespace DocumentProcessing.Manager.Host.Hosting;

internal sealed class NotifyingProcessingResultRegistryWriter(
    IProcessingResultRegistryWriter inner,
    IResultAvailabilitySignal signal)
    : IProcessingResultRegistryWriter
{
    public async ValueTask<ProcessingResultRegistration> RegisterAsync(
        ProcessingResultRecord result,
        CancellationToken cancellationToken = default)
    {
        var registration = await inner.RegisterAsync(result, cancellationToken)
            .ConfigureAwait(false);

        signal.Notify();
        return registration;
    }
}
