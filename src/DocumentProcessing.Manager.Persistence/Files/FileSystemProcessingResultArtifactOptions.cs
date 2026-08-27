namespace DocumentProcessing.Manager.Persistence.Files;

/// <summary>
/// Filesystem processing-result limits and root location.
/// </summary>
public sealed class FileSystemProcessingResultArtifactOptions
{
    #region Properties

    /// <summary>
    /// Gets the absolute managed processing-result root directory.
    /// </summary>
    public string RootDirectory { get; }

    /// <summary>
    /// Gets the maximum accepted processing-result payload length.
    /// </summary>
    public long MaximumArtifactBytes { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates filesystem processing-result artifact options.
    /// </summary>
    public FileSystemProcessingResultArtifactOptions(
        string rootDirectory,
        long maximumArtifactBytes)
    {
        if (string.IsNullOrWhiteSpace(
                rootDirectory))
        {
            throw new ArgumentException(
                "Filesystem processing-result root cannot be empty.",
                nameof(rootDirectory));
        }

        var fullRoot =
            Path.GetFullPath(
                rootDirectory.Trim());

        if (string.Equals(
                fullRoot,
                Path.GetPathRoot(
                    fullRoot),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Filesystem processing-result root cannot be a filesystem root.",
                nameof(rootDirectory));
        }

        if (maximumArtifactBytes <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumArtifactBytes),
                maximumArtifactBytes,
                "Maximum processing-result artifact length must be positive.");
        }

        RootDirectory =
            fullRoot;

        MaximumArtifactBytes =
            maximumArtifactBytes;
    }

    #endregion
}
