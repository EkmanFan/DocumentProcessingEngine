namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Sanitized evidence for one ordinary controlled candidate visual-execution
/// failure.
/// </summary>
public sealed record DocumentControlledCandidateVisualExecutionFailure
{
    public DocumentControlledCandidateVisualExecutionFailure(
        string exceptionType,
        string message,
        int? physicalPageNumber = null,
        int? sourceVisualIndex = null)
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

        if (physicalPageNumber is <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(physicalPageNumber));
        }

        if (sourceVisualIndex is <
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVisualIndex));
        }

        if (sourceVisualIndex.HasValue &&
            !physicalPageNumber.HasValue)
        {
            throw new ArgumentException(
                "A source visual failure requires its physical page number.",
                nameof(sourceVisualIndex));
        }

        ExceptionType =
            exceptionType.Trim();

        Message =
            message.Trim();

        PhysicalPageNumber =
            physicalPageNumber;

        SourceVisualIndex =
            sourceVisualIndex;
    }

    public string ExceptionType { get; }

    public string Message { get; }

    public int? PhysicalPageNumber { get; }

    public int? SourceVisualIndex { get; }
}
