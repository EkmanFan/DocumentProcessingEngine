namespace DocumentProcessing.Epub.Validation;

/// <summary>
/// Internal result of one official EPUBCheck invocation.
/// </summary>
internal enum EpubCheckConformanceStatus
{
    Conformant = 0,
    NonConformant = 1,
    Unavailable = 2,
    Failed = 3,
    TimedOut = 4
}
