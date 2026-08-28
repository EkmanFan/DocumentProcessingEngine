namespace DocumentProcessing.ProviderLifecycle;

/// <summary>
/// Configures the pinned local Docker services owned lazily by
/// <see cref="DocumentProcessingHost"/>.
/// </summary>
public sealed class ManagedDockerProcessingProviderOptions
{
    #region Variables and Constants

    /// <summary>Gets the pinned PP-StructureV3 base image.</summary>
    public const string DefaultLayoutBaseImage =
        "document-processing-ppstructurev3:3.7.0-paddle3.2.2-cpu";

    /// <summary>Gets the pinned PP-StructureV3 serving image.</summary>
    public const string DefaultLayoutServingImage =
        "document-processing-ppstructurev3-serving:3.7.0-paddle3.2.2-cpu";

    /// <summary>Gets the pinned PaddleOCR base image.</summary>
    public const string DefaultOcrBaseImage =
        "document-processing-paddleocr:3.7.0-paddle3.2.2-cpu";

    /// <summary>Gets the pinned PaddleOCR serving image.</summary>
    public const string DefaultOcrServingImage =
        "document-processing-paddleocr-serving:3.7.0-paddle3.2.2-cpu";

    /// <summary>Gets the fallback PP-StructureV3 model-cache volume.</summary>
    public const string DefaultLayoutCacheVolume =
        "dpengine-ppstructurev3-model-cache";

    /// <summary>Gets the fallback PaddleOCR model-cache volume.</summary>
    public const string DefaultOcrCacheVolume =
        "dpengine-paddleocr-model-cache";

    /// <summary>Gets the default memory preflight threshold.</summary>
    public const long DefaultMinimumAvailableMemoryBytes =
        12L *
        1024 *
        1024 *
        1024;

    #endregion

    #region Properties

    /// <summary>Gets the trusted Docker CLI executable.</summary>
    public string DockerExecutable { get; }

    /// <summary>Gets the pinned PP-StructureV3 base image.</summary>
    public string LayoutBaseImage { get; }

    /// <summary>Gets the pinned PP-StructureV3 serving image.</summary>
    public string LayoutServingImage { get; }

    /// <summary>Gets the pinned PaddleOCR base image.</summary>
    public string OcrBaseImage { get; }

    /// <summary>Gets the pinned PaddleOCR serving image.</summary>
    public string OcrServingImage { get; }

    /// <summary>Gets the fallback PP-StructureV3 model-cache volume.</summary>
    public string LayoutCacheVolume { get; }

    /// <summary>Gets the fallback PaddleOCR model-cache volume.</summary>
    public string OcrCacheVolume { get; }

    /// <summary>Gets the per-model Docker memory limit.</summary>
    public string ModelMemoryLimit { get; }

    /// <summary>Gets the per-model shared-memory size.</summary>
    public string SharedMemorySize { get; }

    /// <summary>Gets the memory required before the first owned model starts.</summary>
    public long MinimumAvailableMemoryBytes { get; }

    /// <summary>Gets the maximum provider readiness wait.</summary>
    public TimeSpan StartupTimeout { get; }

    /// <summary>Gets the provider readiness polling interval.</summary>
    public TimeSpan ReadinessPollingInterval { get; }

    /// <summary>Gets the timeout for ordinary Docker commands.</summary>
    public TimeSpan CommandTimeout { get; }

    /// <summary>Gets the timeout for provider image builds.</summary>
    public TimeSpan ImageBuildTimeout { get; }

    /// <summary>Gets the optional absolute DPEngine repository root.</summary>
    public string? RepositoryRoot { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Initializes managed Docker lifecycle options. The repository root is
    /// required only when pinned provider images must be built locally.
    /// </summary>
    public ManagedDockerProcessingProviderOptions(
        string dockerExecutable = "docker",
        string layoutBaseImage = DefaultLayoutBaseImage,
        string layoutServingImage = DefaultLayoutServingImage,
        string ocrBaseImage = DefaultOcrBaseImage,
        string ocrServingImage = DefaultOcrServingImage,
        string layoutCacheVolume = DefaultLayoutCacheVolume,
        string ocrCacheVolume = DefaultOcrCacheVolume,
        string modelMemoryLimit = "12g",
        string sharedMemorySize = "2g",
        long minimumAvailableMemoryBytes = DefaultMinimumAvailableMemoryBytes,
        TimeSpan? startupTimeout = null,
        TimeSpan? readinessPollingInterval = null,
        TimeSpan? commandTimeout = null,
        TimeSpan? imageBuildTimeout = null,
        string? repositoryRoot = null)
    {
        DockerExecutable =
            RequireToken(
                dockerExecutable,
                nameof(dockerExecutable));

        LayoutBaseImage =
            RequireDockerReference(
                layoutBaseImage,
                nameof(layoutBaseImage));

        LayoutServingImage =
            RequireDockerReference(
                layoutServingImage,
                nameof(layoutServingImage));

        OcrBaseImage =
            RequireDockerReference(
                ocrBaseImage,
                nameof(ocrBaseImage));

        OcrServingImage =
            RequireDockerReference(
                ocrServingImage,
                nameof(ocrServingImage));

        LayoutCacheVolume =
            RequireDockerReference(
                layoutCacheVolume,
                nameof(layoutCacheVolume));

        OcrCacheVolume =
            RequireDockerReference(
                ocrCacheVolume,
                nameof(ocrCacheVolume));

        ModelMemoryLimit =
            RequireToken(
                modelMemoryLimit,
                nameof(modelMemoryLimit));

        SharedMemorySize =
            RequireToken(
                sharedMemorySize,
                nameof(sharedMemorySize));

        if (minimumAvailableMemoryBytes <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumAvailableMemoryBytes),
                minimumAvailableMemoryBytes,
                "Minimum available memory cannot be negative.");
        }

        MinimumAvailableMemoryBytes =
            minimumAvailableMemoryBytes;

        StartupTimeout =
            RequirePositiveDuration(
                startupTimeout ??
                TimeSpan.FromMinutes(
                    20),
                nameof(startupTimeout));

        ReadinessPollingInterval =
            RequirePositiveDuration(
                readinessPollingInterval ??
                TimeSpan.FromSeconds(
                    2),
                nameof(readinessPollingInterval));

        if (ReadinessPollingInterval >=
            StartupTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(readinessPollingInterval),
                "Readiness polling must be shorter than the startup timeout.");
        }

        CommandTimeout =
            RequirePositiveDuration(
                commandTimeout ??
                TimeSpan.FromMinutes(
                    2),
                nameof(commandTimeout));

        ImageBuildTimeout =
            RequirePositiveDuration(
                imageBuildTimeout ??
                TimeSpan.FromHours(
                    1),
                nameof(imageBuildTimeout));

        if (repositoryRoot is not null &&
            !Path.IsPathFullyQualified(
                repositoryRoot))
        {
            throw new ArgumentException(
                "Provider repository root must be an absolute path.",
                nameof(repositoryRoot));
        }

        RepositoryRoot =
            repositoryRoot is null
                ? null
                : Path.GetFullPath(
                    repositoryRoot);
    }

    #endregion

    #region Methods Validation

    private static string RequireToken(
        string value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(
                value) ||
            value.Any(
                char.IsControl))
        {
            throw new ArgumentException(
                "Managed provider option cannot be empty or contain control characters.",
                parameterName);
        }

        return value.Trim();
    }

    private static string RequireDockerReference(
        string value,
        string parameterName)
    {
        var normalized =
            RequireToken(
                value,
                parameterName);

        if (normalized.Any(
                character =>
                    !(char.IsAsciiLetterOrDigit(
                          character) ||
                      character is
                          '.' or
                          '_' or
                          '-' or
                          '/' or
                          ':' or
                          '@')))
        {
            throw new ArgumentException(
                "Docker image and volume references contain unsupported characters.",
                parameterName);
        }

        return normalized;
    }

    private static TimeSpan RequirePositiveDuration(
        TimeSpan value,
        string parameterName)
    {
        if (value <=
                TimeSpan.Zero ||
            value ==
                Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Duration must be finite and greater than zero.");
        }

        return value;
    }

    #endregion
}
