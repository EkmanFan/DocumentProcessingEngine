namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Sanitized ordinary failure evidence for H.4D.4 comparison.
/// </summary>
public sealed record DocumentControlledCandidateComparisonFailure
{
    public DocumentControlledCandidateComparisonFailure(
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
