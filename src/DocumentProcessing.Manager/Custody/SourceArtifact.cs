namespace DocumentProcessing.Manager.Custody;

/// <summary>
/// Immutable identity and length of one preserved source artifact.
/// </summary>
public sealed record SourceArtifact
{
    #region Properties

    /// <summary>
    /// Gets the content-addressed SHA-256 identity.
    /// </summary>
    public Sha256Digest Digest { get; }

    /// <summary>
    /// Gets the exact number of preserved source bytes.
    /// </summary>
    public long ByteLength { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates an immutable source-artifact descriptor.
    /// </summary>
    public SourceArtifact(
        Sha256Digest digest,
        long byteLength)
    {
        if (string.IsNullOrWhiteSpace(
                digest.Value))
        {
            throw new ArgumentException(
                "Source artifact requires a valid SHA-256 digest.",
                nameof(digest));
        }

        if (byteLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteLength),
                byteLength,
                "Source artifact must contain at least one byte.");
        }

        Digest =
            digest;

        ByteLength =
            byteLength;
    }

    #endregion
}
