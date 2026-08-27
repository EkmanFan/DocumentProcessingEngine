namespace DocumentProcessing.Manager.Processing;

/// <summary>
/// Durable description of one terminal processing failure.
/// </summary>
public sealed record ProcessingFailure
{
    #region Properties

    /// <summary>
    /// Gets the stable failure code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the failure message retained for diagnostics.
    /// </summary>
    public string Message { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates one durable processing failure.
    /// </summary>
    public ProcessingFailure(
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

    #region Methods Factories

    internal static ProcessingFailure From(
        ProcessingExecutionOutcome.Failure failure) =>
        new(
            failure.Code,
            failure.Message);

    internal static ProcessingFailure From(
        Exception exception) =>
        new(
            exception.GetType().FullName ??
            exception.GetType().Name,
            string.IsNullOrWhiteSpace(
                exception.Message)
                ? "Technical document-processing failure."
                : exception.Message);

    #endregion
}
