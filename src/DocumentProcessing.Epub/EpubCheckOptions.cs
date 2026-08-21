namespace DocumentProcessing.Epub;

/// <summary>
/// Runtime configuration for the official EPUBCheck distribution.
/// </summary>
public sealed class EpubCheckOptions
{
    #region Variables and Constants

    public const string SupportedVersion =
        "5.3.0";

    public static readonly TimeSpan DefaultTimeout =
        TimeSpan.FromMinutes(
            2);

    #endregion

    #region Properties

    /// <summary>
    /// Gets the directory containing epubcheck.jar and its lib directory.
    /// </summary>
    public string DistributionDirectoryPath { get; }

    /// <summary>
    /// Gets the Java executable path or command name.
    /// </summary>
    public string JavaExecutablePath { get; }

    /// <summary>
    /// Gets the maximum duration of one conformance check.
    /// </summary>
    public TimeSpan Timeout { get; }

    internal string EpubCheckJarPath =>
        Path.Combine(
            DistributionDirectoryPath,
            "epubcheck.jar");

    #endregion

    #region ctor

    public static EpubCheckOptions CreateDefault() =>
        new(
            Path.Combine(
                AppContext.BaseDirectory,
                "epubcheck",
                SupportedVersion));

    public EpubCheckOptions(
        string distributionDirectoryPath,
        string javaExecutablePath = "java",
        TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(
                distributionDirectoryPath))
        {
            throw new ArgumentException(
                "EPUBCheck distribution directory cannot be empty.",
                nameof(distributionDirectoryPath));
        }

        if (string.IsNullOrWhiteSpace(
                javaExecutablePath))
        {
            throw new ArgumentException(
                "Java executable path cannot be empty.",
                nameof(javaExecutablePath));
        }

        var effectiveTimeout =
            timeout ??
            DefaultTimeout;

        if (effectiveTimeout <=
            TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "EPUBCheck timeout must be positive.");
        }

        DistributionDirectoryPath =
            Path.GetFullPath(
                distributionDirectoryPath.Trim());

        JavaExecutablePath =
            javaExecutablePath.Trim();

        Timeout =
            effectiveTimeout;
    }

    #endregion
}
