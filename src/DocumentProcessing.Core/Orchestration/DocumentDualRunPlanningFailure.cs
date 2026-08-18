namespace DocumentProcessing.Core.Orchestration;

/// <summary>
/// Sanitized deterministic description of a non-fatal shadow-planning failure.
/// </summary>
public sealed record DocumentDualRunPlanningFailure
{
    public DocumentDualRunPlanningFailure(
        DocumentDualRunPlanningFailureStage stage,
        string exceptionType,
        string message)
    {
        if (!Enum.IsDefined(
                stage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(stage),
                stage,
                "Shadow-planning failure stage must be defined.");
        }

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

        Stage =
            stage;

        ExceptionType =
            exceptionType.Trim();

        Message =
            message.Trim();
    }

    public DocumentDualRunPlanningFailureStage Stage { get; }

    public string ExceptionType { get; }

    public string Message { get; }
}
