namespace DocumentProcessing.Core.DualRun;
/// <summary>
/// Sanitized evidence for an ordinary Dual Run candidate execution failure.
/// </summary>
public sealed record DocumentDualRunCandidateTextExecutionFailure
{
    public DocumentDualRunCandidateTextExecutionFailure(
        string exceptionType,
        string message,
        int? physicalPageNumber = null)
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

        if (physicalPageNumber is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        ExceptionType =
            exceptionType.Trim();

        Message =
            message.Trim();

        PhysicalPageNumber =
            physicalPageNumber;
    }

    public string ExceptionType { get; }

    public string Message { get; }

    public int? PhysicalPageNumber { get; }
}
