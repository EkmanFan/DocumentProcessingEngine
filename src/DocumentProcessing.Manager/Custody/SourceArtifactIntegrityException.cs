namespace DocumentProcessing.Manager.Custody;

/// <summary>
/// Reports that retained bytes no longer match their immutable descriptor.
/// </summary>
public sealed class SourceArtifactIntegrityException
    : IOException
{
    #region Properties

    /// <summary>
    /// Gets the expected content digest.
    /// </summary>
    public Sha256Digest ExpectedDigest { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a source-artifact integrity exception.
    /// </summary>
    public SourceArtifactIntegrityException(
        Sha256Digest expectedDigest,
        string message)
        : base(
            message)
    {
        ExpectedDigest =
            expectedDigest;
    }

    #endregion
}
