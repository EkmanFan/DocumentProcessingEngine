namespace DocumentProcessing.Epub.Validation;

/// <summary>
/// Deliberately contains no process output or exception detail that could leak
/// into a consumer-facing processing result.
/// </summary>
internal sealed record EpubCheckConformanceResult(
    EpubCheckConformanceStatus Status);
