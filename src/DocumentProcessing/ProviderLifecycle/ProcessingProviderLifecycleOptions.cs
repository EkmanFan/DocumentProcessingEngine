namespace DocumentProcessing.ProviderLifecycle;

/// <summary>
/// Selects the shared processing-provider lifecycle strategy composed by
/// <see cref="DocumentProcessingHost"/>.
/// </summary>
public sealed class ProcessingProviderLifecycleOptions
{
    #region Properties

    /// <summary>
    /// Gets the selected lifecycle strategy.
    /// </summary>
    public ProcessingProviderLifecycleMode Mode { get; }

    /// <summary>
    /// Gets Docker-specific lifecycle options when <see cref="Mode"/> is
    /// <see cref="ProcessingProviderLifecycleMode.ManagedDocker"/>.
    /// </summary>
    public ManagedDockerProcessingProviderOptions? ManagedDocker { get; }

    /// <summary>
    /// Gets a strategy that consumes already-managed provider endpoints.
    /// </summary>
    public static ProcessingProviderLifecycleOptions External { get; } =
        new(
            ProcessingProviderLifecycleMode.External,
            managedDocker:
                null);

    #endregion

    #region ctor

    private ProcessingProviderLifecycleOptions(
        ProcessingProviderLifecycleMode mode,
        ManagedDockerProcessingProviderOptions? managedDocker)
    {
        Mode =
            mode;

        ManagedDocker =
            managedDocker;
    }

    #endregion

    #region Methods Factory

    /// <summary>
    /// Creates a strategy that lazily owns pinned local Docker providers.
    /// </summary>
    public static ProcessingProviderLifecycleOptions CreateManagedDocker(
        ManagedDockerProcessingProviderOptions? options = null) =>
        new(
            ProcessingProviderLifecycleMode.ManagedDocker,
            options ??
            new ManagedDockerProcessingProviderOptions());

    #endregion
}
