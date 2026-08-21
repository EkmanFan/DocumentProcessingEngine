namespace DocumentProcessing.Epub.Validation;

internal enum EpubCheckProcessOutcome
{
    Completed = 0,
    Unavailable = 1,
    Failed = 2,
    TimedOut = 3
}

internal sealed record EpubCheckProcessResult(
    EpubCheckProcessOutcome Outcome,
    int? ExitCode = null,
    string StandardOutput = "",
    string StandardError = "",
    Exception? Exception = null);
