namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Sanitized ordinary H.4D.4B.1 projection failure evidence.
/// </summary>
public sealed record DocumentControlledCandidatePortableProjectionFailure
{
    public DocumentControlledCandidatePortableProjectionFailure(
        string exceptionType,
        string message)
    {
        if (string.IsNullOrWhiteSpace(
                exceptionType))
        {
            throw new ArgumentException(
                "Exception type cannot be empty.",
                nameof(exceptionType));
        }

        if (string.IsNullOrWhiteSpace(
                message))
        {
            throw new ArgumentException(
                "Failure message cannot be empty.",
                nameof(message));
        }

        ExceptionType =
            exceptionType.Trim();

        Message =
            message.Trim();
    }

    public string ExceptionType { get; }

    public string Message { get; }
}
