namespace DocumentProcessing.Manager.Persistence.Files;

/// <summary>
/// Filesystem custody limits and root location.
/// </summary>
public sealed class FileSystemSourceArtifactCustodyOptions
{
    #region Properties

    /// <summary>
    /// Gets the absolute managed custody root directory.
    /// </summary>
    public string RootDirectory { get; }

    /// <summary>
    /// Gets the maximum accepted source-artifact length.
    /// </summary>
    public long MaximumArtifactBytes { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates filesystem source-custody options.
    /// </summary>
    public FileSystemSourceArtifactCustodyOptions(
        string rootDirectory,
        long maximumArtifactBytes)
    {
        if (string.IsNullOrWhiteSpace(
                rootDirectory))
        {
            throw new ArgumentException(
                "Filesystem custody root cannot be empty.",
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
                "Filesystem custody root cannot be a filesystem root.",
                nameof(rootDirectory));
        }

        if (maximumArtifactBytes <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumArtifactBytes),
                maximumArtifactBytes,
                "Maximum source-artifact length must be positive.");
        }

        RootDirectory =
            fullRoot;

        MaximumArtifactBytes =
            maximumArtifactBytes;
    }

    #endregion
}
