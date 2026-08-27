using DocumentProcessing.Manager.Custody;

namespace DocumentProcessing.Manager.Results;

/// <summary>
/// Reports that durable result bytes no longer match their descriptor.
/// </summary>
public sealed class ProcessingResultIntegrityException
    : IOException
{
    #region Properties

    /// <summary>
    /// Gets the expected result-content digest.
    /// </summary>
    public Sha256Digest ExpectedDigest { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a processing-result integrity exception.
    /// </summary>
    public ProcessingResultIntegrityException(
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
