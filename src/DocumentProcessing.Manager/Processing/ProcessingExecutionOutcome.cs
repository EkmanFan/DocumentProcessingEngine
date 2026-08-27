namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Functional outcome returned by a document-processing adapter.
/// </summary>
public abstract record ProcessingExecutionOutcome
{
    private ProcessingExecutionOutcome()
    {
    }

    /// <summary>
    /// Represents successful durable production of a processing result.
    /// </summary>
    public sealed record Success
        : ProcessingExecutionOutcome
    {
        #region Properties

        /// <summary>
        /// Gets the opaque durable result reference.
        /// </summary>
        public string ResultReference { get; }

        #endregion

        #region ctor

        /// <summary>
        /// Creates a successful processing outcome.
        /// </summary>
        public Success(
            string resultReference)
        {
            if (string.IsNullOrWhiteSpace(
                    resultReference))
            {
                throw new ArgumentException(
                    "Successful processing requires a durable result reference.",
                    nameof(resultReference));
            }

            ResultReference =
                resultReference.Trim();
        }

        #endregion
    }

    /// <summary>
    /// Represents a terminal functional processing failure.
    /// </summary>
    public sealed record Failure
        : ProcessingExecutionOutcome
    {
        #region Properties

        /// <summary>
        /// Gets the stable failure code.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the consumer-safe failure message.
        /// </summary>
        public string Message { get; }

        #endregion

        #region ctor

        /// <summary>
        /// Creates a terminal functional failure.
        /// </summary>
        public Failure(
            string code,
            string message)
        {
            if (string.IsNullOrWhiteSpace(
                    code))
            {
                throw new ArgumentException(
                    "Processing failure code cannot be empty.",
                    nameof(code));
            }

            if (string.IsNullOrWhiteSpace(
                    message))
            {
                throw new ArgumentException(
                    "Processing failure message cannot be empty.",
                    nameof(message));
            }

            Code =
                code.Trim();

            Message =
                message.Trim();
        }

        #endregion
    }
}
