using DocumentProcessing.Layout.Adapters.PpStructureV3;
using DocumentProcessing.Ocr.Adapters.PaddleOCR;
using Microsoft.Extensions.Logging;

namespace DocumentProcessing.ProviderLifecycle;

internal static class ProcessingProviderRuntimeFactory
{
    #region Methods

    public static IProcessingProviderRuntime Create(
        ProcessingProviderLifecycleOptions lifecycle,
        PpStructureV3Options layout,
        PaddleOcrOptions ocr,
        ILoggerFactory? loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(
            lifecycle);

        ArgumentNullException.ThrowIfNull(
            layout);

        ArgumentNullException.ThrowIfNull(
            ocr);

        return lifecycle.Mode switch
        {
            ProcessingProviderLifecycleMode.External =>
                new ExternalProcessingProviderRuntime(),
            ProcessingProviderLifecycleMode.ManagedDocker
                when lifecycle.ManagedDocker is not null =>
                new ManagedDockerProcessingProviderRuntime(
                    lifecycle.ManagedDocker,
                    layout.Endpoint,
                    ocr.Endpoint,
                    loggerFactory),
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(lifecycle),
                    lifecycle.Mode,
                    "Unknown or incomplete processing-provider lifecycle strategy.")
        };
    }

    #endregion
}
