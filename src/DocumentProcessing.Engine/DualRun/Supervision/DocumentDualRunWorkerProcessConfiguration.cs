namespace DocumentProcessing.Engine.DualRun.Supervision;

/// <summary>
/// Explicit parent-side process limits.
///
/// Timeout intentionally has no production default. Phase 16.4 must provide
/// evidence before a deployment chooses one.
/// </summary>
public sealed record DocumentDualRunWorkerProcessConfiguration
{
    #region ctor

    public DocumentDualRunWorkerProcessConfiguration(
        string workerExecutablePath,
        TimeSpan? timeout,
        TimeSpan terminationGracePeriod,
        long maximumRequestFileBytes,
        long maximumResultFileBytes,
        int maximumCapturedStandardErrorCharacters)
    {
        if (string.IsNullOrWhiteSpace(
                workerExecutablePath))
        {
            throw new ArgumentException(
                "Dual Run worker executable path cannot be empty.",
                nameof(workerExecutablePath));
        }

        if (!Path.IsPathFullyQualified(
                workerExecutablePath))
        {
            throw new ArgumentException(
                "Dual Run worker executable path must be fully qualified.",
                nameof(workerExecutablePath));
        }

        if (timeout <=
            TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout));
        }

        if (terminationGracePeriod <=
            TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(terminationGracePeriod));
        }

        if (maximumRequestFileBytes <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRequestFileBytes));
        }

        if (maximumResultFileBytes <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResultFileBytes));
        }

        if (maximumCapturedStandardErrorCharacters <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCapturedStandardErrorCharacters));
        }

        WorkerExecutablePath =
            Path.GetFullPath(
                workerExecutablePath);

        Timeout =
            timeout;

        TerminationGracePeriod =
            terminationGracePeriod;

        MaximumRequestFileBytes =
            maximumRequestFileBytes;

        MaximumResultFileBytes =
            maximumResultFileBytes;

        MaximumCapturedStandardErrorCharacters =
            maximumCapturedStandardErrorCharacters;
    }

    #endregion

    #region Properties

    public string WorkerExecutablePath { get; }

    public TimeSpan? Timeout { get; }

    public TimeSpan TerminationGracePeriod { get; }

    public long MaximumRequestFileBytes { get; }

    public long MaximumResultFileBytes { get; }

    public int MaximumCapturedStandardErrorCharacters { get; }

    #endregion
}
