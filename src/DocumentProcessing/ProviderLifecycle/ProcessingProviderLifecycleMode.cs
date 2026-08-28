namespace DocumentProcessing.ProviderLifecycle;

/// <summary>
/// Defines who owns the lifecycle of shared Layout/OCR provider services.
/// </summary>
public enum ProcessingProviderLifecycleMode
{
    /// <summary>
    /// Uses provider endpoints whose deployment and lifecycle are owned by the
    /// embedding infrastructure.
    /// </summary>
    External = 0,

    /// <summary>
    /// Lets <see cref="DocumentProcessingHost"/> lazily start and stop pinned
    /// local Docker providers when processing first requires them.
    /// </summary>
    ManagedDocker = 1
}
