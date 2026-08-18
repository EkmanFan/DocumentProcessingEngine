using DocumentProcessing.Core.DualRun;

namespace DocumentProcessing.Engine.DualRun.Submission;

public enum DocumentDualRunSubmissionStatus
{
    NotSelected,
    Enqueued,
    QueueFull,
    DispatcherStopped,
    Cancelled,
    Failed
}

public enum DocumentDualRunSubmissionFailureStage
{
    DispatcherResolution,
    SelectedSubmissionCreation,
    SourceSnapshot,
    RequestPreparation,
    Dispatch
}

/// <summary>
/// Sanitized non-authoritative submission failure evidence.
/// </summary>
public sealed record DocumentDualRunSubmissionFailure
{
    #region Variables and Constants

    private const int MaximumMessageLength =
        1024;

    #endregion

    #region ctor

    public DocumentDualRunSubmissionFailure(
        DocumentDualRunSubmissionFailureStage stage,
        string exceptionType,
        string message)
    {
        if (!Enum.IsDefined(
                typeof(DocumentDualRunSubmissionFailureStage),
                stage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(stage));
        }

        if (string.IsNullOrWhiteSpace(
                exceptionType))
        {
            throw new ArgumentException(
                "Submission failure exception type cannot be empty.",
                nameof(exceptionType));
        }

        Stage =
            stage;

        ExceptionType =
            exceptionType.Trim();

        var normalizedMessage =
            string.IsNullOrWhiteSpace(
                message)
                ? "<no message>"
                : message.Trim();

        Message =
            normalizedMessage.Length <=
                MaximumMessageLength
                ? normalizedMessage
                : normalizedMessage[
                    ..MaximumMessageLength];
    }

    #endregion

    #region Properties

    public DocumentDualRunSubmissionFailureStage Stage { get; }

    public string ExceptionType { get; }

    public string Message { get; }

    #endregion

    #region Methods Factory

    internal static DocumentDualRunSubmissionFailure FromException(
        DocumentDualRunSubmissionFailureStage stage,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(
            exception);

        return new DocumentDualRunSubmissionFailure(
            stage,
            exception.GetType().FullName ??
            exception.GetType().Name,
            exception.Message);
    }

    #endregion
}

/// <summary>
/// Non-authoritative outcome of one document-level Dual Run submission attempt.
/// </summary>
public sealed record DocumentDualRunSubmissionResult
{
    #region ctor

    private DocumentDualRunSubmissionResult(
        DocumentDualRunSubmissionStatus status,
        DocumentDualRunSelection selection,
        Guid? jobId,
        DocumentDualRunSubmissionFailure? failure)
    {
        ArgumentNullException.ThrowIfNull(
            selection);

        if (!Enum.IsDefined(
                typeof(DocumentDualRunSubmissionStatus),
                status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status));
        }

        if (status ==
                DocumentDualRunSubmissionStatus.NotSelected &&
            selection.IsSelected)
        {
            throw new ArgumentException(
                "NotSelected result requires an unselected document.",
                nameof(selection));
        }

        if (status !=
                DocumentDualRunSubmissionStatus.NotSelected &&
            !selection.IsSelected)
        {
            throw new ArgumentException(
                "Selected submission outcomes require a selected document.",
                nameof(selection));
        }

        var requiresJobId =
            status is
                DocumentDualRunSubmissionStatus.Enqueued or
                DocumentDualRunSubmissionStatus.QueueFull or
                DocumentDualRunSubmissionStatus.DispatcherStopped;

        if (requiresJobId !=
            jobId.HasValue)
        {
            throw new ArgumentException(
                "Submission job ID presence does not match the outcome.",
                nameof(jobId));
        }

        if ((status ==
             DocumentDualRunSubmissionStatus.Failed) !=
            (failure is not null))
        {
            throw new ArgumentException(
                "Only Failed submission results carry failure evidence.",
                nameof(failure));
        }

        Status =
            status;

        Selection =
            selection;

        JobId =
            jobId;

        Failure =
            failure;
    }

    #endregion

    #region Properties

    public DocumentDualRunSubmissionStatus Status { get; }

    public DocumentDualRunSelection Selection { get; }

    public Guid? JobId { get; }

    public DocumentDualRunSubmissionFailure? Failure { get; }

    #endregion

    #region Methods Factories

    internal static DocumentDualRunSubmissionResult NotSelected(
        DocumentDualRunSelection selection) =>
        new(
            DocumentDualRunSubmissionStatus.NotSelected,
            selection,
            jobId:
                null,
            failure:
                null);

    internal static DocumentDualRunSubmissionResult Enqueued(
        DocumentDualRunSelection selection,
        Guid jobId) =>
        new(
            DocumentDualRunSubmissionStatus.Enqueued,
            selection,
            jobId,
            failure:
                null);

    internal static DocumentDualRunSubmissionResult QueueFull(
        DocumentDualRunSelection selection,
        Guid jobId) =>
        new(
            DocumentDualRunSubmissionStatus.QueueFull,
            selection,
            jobId,
            failure:
                null);

    internal static DocumentDualRunSubmissionResult DispatcherStopped(
        DocumentDualRunSelection selection,
        Guid jobId) =>
        new(
            DocumentDualRunSubmissionStatus.DispatcherStopped,
            selection,
            jobId,
            failure:
                null);

    internal static DocumentDualRunSubmissionResult Cancelled(
        DocumentDualRunSelection selection) =>
        new(
            DocumentDualRunSubmissionStatus.Cancelled,
            selection,
            jobId:
                null,
            failure:
                null);

    internal static DocumentDualRunSubmissionResult Failed(
        DocumentDualRunSelection selection,
        DocumentDualRunSubmissionFailure failure) =>
        new(
            DocumentDualRunSubmissionStatus.Failed,
            selection,
            jobId:
                null,
            failure);

    #endregion
}
