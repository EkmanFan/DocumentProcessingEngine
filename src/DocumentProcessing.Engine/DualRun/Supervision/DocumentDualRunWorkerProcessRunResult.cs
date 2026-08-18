using DocumentProcessing.Core.DualRun.Transport;

namespace DocumentProcessing.Engine.DualRun.Supervision;

public enum DocumentDualRunWorkerProcessOutcome
{
    ResultReceived,
    InvalidJob,
    LaunchFailed,
    TimedOut,
    Cancelled,
    NonZeroExit,
    MissingResult,
    InvalidResult,
    SupervisionFailed
}

/// <summary>
/// Parent-side process evidence. Worker stderr is untrusted and size-capped.
/// </summary>
public sealed record DocumentDualRunWorkerProcessRunResult
{
    #region Variables and Constants

    private const int MaximumFailureMessageLength =
        1024;

    #endregion

    #region ctor

    private DocumentDualRunWorkerProcessRunResult(
        DocumentDualRunWorkerProcessOutcome outcome,
        int? exitCode,
        DocumentDualRunWorkerResult? workerResult,
        string standardError,
        string? failureType,
        string? failureMessage,
        bool processTreeKillAttempted,
        bool processTerminationConfirmed)
    {
        Outcome =
            outcome;

        ExitCode =
            exitCode;

        WorkerResult =
            workerResult;

        StandardError =
            standardError;

        FailureType =
            failureType;

        FailureMessage =
            failureMessage;

        ProcessTreeKillAttempted =
            processTreeKillAttempted;

        ProcessTerminationConfirmed =
            processTerminationConfirmed;
    }

    #endregion

    #region Properties

    public DocumentDualRunWorkerProcessOutcome Outcome { get; }

    public int? ExitCode { get; }

    public DocumentDualRunWorkerResult? WorkerResult { get; }

    public string StandardError { get; }

    public string? FailureType { get; }

    public string? FailureMessage { get; }

    public bool ProcessTreeKillAttempted { get; }

    public bool ProcessTerminationConfirmed { get; }

    #endregion

    #region Methods Factories

    internal static DocumentDualRunWorkerProcessRunResult ResultReceived(
        int exitCode,
        DocumentDualRunWorkerResult workerResult,
        string standardError) =>
        new(
            DocumentDualRunWorkerProcessOutcome.ResultReceived,
            exitCode,
            workerResult,
            standardError,
            failureType:
                null,
            failureMessage:
                null,
            processTreeKillAttempted:
                false,
            processTerminationConfirmed:
                true);

    internal static DocumentDualRunWorkerProcessRunResult WithoutResult(
        DocumentDualRunWorkerProcessOutcome outcome,
        int? exitCode,
        string standardError,
        Exception? failure = null,
        bool processTreeKillAttempted = false,
        bool processTerminationConfirmed = false)
    {
        if (outcome ==
            DocumentDualRunWorkerProcessOutcome.ResultReceived)
        {
            throw new ArgumentException(
                "ResultReceived requires worker result evidence.",
                nameof(outcome));
        }

        return new DocumentDualRunWorkerProcessRunResult(
            outcome,
            exitCode,
            workerResult:
                null,
            standardError,
            failure?.GetType().FullName ??
            failure?.GetType().Name,
            failure is null
                ? null
                : SanitizeFailureMessage(
                    failure.Message),
            processTreeKillAttempted,
            processTerminationConfirmed);
    }

    private static string SanitizeFailureMessage(
        string? value)
    {
        var normalized =
            string.IsNullOrWhiteSpace(
                value)
                ? "<no message>"
                : value
                    .Replace(
                        '\r',
                        ' ')
                    .Replace(
                        '\n',
                        ' ')
                    .Trim();

        return normalized.Length <=
            MaximumFailureMessageLength
            ? normalized
            : normalized[
                ..MaximumFailureMessageLength];
    }

    #endregion
}
