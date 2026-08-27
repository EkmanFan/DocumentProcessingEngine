namespace DocumentProcessing.Manager.Results;

/// <summary>
/// Durable idempotent outcome of registering one processing result.
/// </summary>
public sealed record ProcessingResultRegistration
{
    #region Properties

    /// <summary>
    /// Gets the durable canonical result record.
    /// </summary>
    public ProcessingResultRecord Result { get; }

    /// <summary>
    /// Gets whether this call created the durable registry entry.
    /// </summary>
    public bool Created { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one processing-result registration outcome.
    /// </summary>
    public ProcessingResultRegistration(
        ProcessingResultRecord result,
        bool created)
    {
        Result =
            result ??
            throw new ArgumentNullException(
                nameof(result));

        Created =
            created;
    }

    #endregion
}
