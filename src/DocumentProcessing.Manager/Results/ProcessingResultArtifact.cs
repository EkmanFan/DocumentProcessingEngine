using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Results;

/// <summary>
/// Immutable identity and length of one durable processing-result payload.
/// </summary>
public sealed record ProcessingResultArtifact
{
    #region Properties

    /// <summary>
    /// Gets the content-addressed SHA-256 identity.
    /// </summary>
    public Sha256Digest Digest { get; }

    /// <summary>
    /// Gets the exact number of durable result bytes.
    /// </summary>
    public long ByteLength { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates an immutable processing-result artifact descriptor.
    /// </summary>
    public ProcessingResultArtifact(
        Sha256Digest digest,
        long byteLength)
    {
        if (string.IsNullOrWhiteSpace(
                digest.Value))
        {
            throw new ArgumentException(
                "Processing-result artifact requires a valid SHA-256 digest.",
                nameof(digest));
        }

        if (byteLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteLength),
                byteLength,
                "Processing-result artifact must contain at least one byte.");
        }

        Digest =
            digest;

        ByteLength =
            byteLength;
    }

    #endregion
}
