using DocumentProcessing.Manager.Runtime;

namespace DocumentProcessing.Manager.Host.Hosting;

internal sealed class ManagerRuntimeHostedService(
    DocumentProcessingManagerRuntime runtime)
    : BackgroundService
{
    #region Variables and Constants

    private readonly DocumentProcessingManagerRuntime
        _runtime =
            runtime ??
            throw new ArgumentNullException(
                nameof(runtime));

    #endregion

    #region Methods

    protected override Task ExecuteAsync(
        CancellationToken stoppingToken) =>
        _runtime.RunAsync(
            stoppingToken);

    #endregion
}
